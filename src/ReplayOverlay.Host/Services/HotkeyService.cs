using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ReplayOverlay.Host.Models;

namespace ReplayOverlay.Host.Services;

public class HotkeyService : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    // Must be stored as a field to prevent GC while the hook is active
    private NativeMethods.LowLevelKeyboardProc? _hookProc;

    private uint _toggleVk;
    private uint _toggleMod;
    private uint _saveVk;
    private uint _saveMod;
    private bool _registered;

    private const double ToggleDebounceS = 0.3;
    private DateTime _lastToggle = DateTime.MinValue;
    private DateTime _lastSave = DateTime.MinValue;

    // Diagnostic: count hook callbacks to verify hook is alive
    private int _hookCallbackCount;

    public event Action? ToggleHotkeyPressed;
    public event Action? SaveHotkeyPressed;

    public void Initialize()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookProc = HookCallback;

        // Try managed module handle first, then IntPtr.Zero (works on .NET 5+ single-file)
        var hMod = Marshal.GetHINSTANCE(typeof(HotkeyService).Module);
        Log($"Initialize: hMod=0x{hMod:X}, thread={Environment.CurrentManagedThreadId}");

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            hMod,
            0);

        if (_hookId == IntPtr.Zero)
        {
            var err1 = Marshal.GetLastWin32Error();
            Log($"Initialize: first attempt FAILED (error={err1}), retrying with IntPtr.Zero");
            _hookId = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL, _hookProc, IntPtr.Zero, 0);

            if (_hookId == IntPtr.Zero)
            {
                var err2 = Marshal.GetLastWin32Error();
                Log($"Initialize: second attempt FAILED (error={err2})");
            }
        }

        Log($"Initialize: hookId=0x{_hookId:X} ({(_hookId != IntPtr.Zero ? "OK" : "FAIL")})");
    }

    /// <summary>
    /// Registers hotkey combinations to listen for. Returns (toggleOk, saveOk).
    /// </summary>
    public (bool toggleOk, bool saveOk) Register(string toggleKey, string saveKey)
    {
        Unregister();

        bool toggleOk = ParseHotkey(toggleKey, out _toggleMod, out _toggleVk);
        bool saveOk = ParseHotkey(saveKey, out _saveMod, out _saveVk);

        if (!toggleOk)
            Trace.WriteLine($"ParseHotkey failed for toggle '{toggleKey}'");
        if (!saveOk)
            Trace.WriteLine($"ParseHotkey failed for save '{saveKey}'");

        _registered = toggleOk || saveOk;
        Trace.WriteLine($"Hotkeys configured: toggle={toggleKey}(vk=0x{_toggleVk:X2},{(toggleOk ? "OK" : "FAIL")}), " +
                         $"save={saveKey}(vk=0x{_saveVk:X2},{(saveOk ? "OK" : "FAIL")})");
        return (toggleOk, saveOk);
    }

    public void Unregister()
    {
        _toggleVk = 0;
        _toggleMod = 0;
        _saveVk = 0;
        _saveMod = 0;
        _registered = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= NativeMethods.HC_ACTION && _registered)
        {
            int msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                // Log first 5 keystrokes to confirm hook is alive
                if (_hookCallbackCount < 5)
                {
                    _hookCallbackCount++;
                    Log($"HookCallback: vk=0x{kbd.vkCode:X2} (count={_hookCallbackCount})");
                }

                CheckHotkey(kbd.vkCode);
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void CheckHotkey(uint vkCode)
    {
        // Check toggle hotkey
        if (_toggleVk != 0 && vkCode == _toggleVk)
        {
            bool modMatch = ModifiersMatch(_toggleMod);
            Log($"Toggle key matched vk=0x{vkCode:X2}, modMatch={modMatch}");
            if (modMatch)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastToggle).TotalSeconds >= ToggleDebounceS)
                {
                    _lastToggle = now;
                    Log("TOGGLE HOTKEY FIRED");
                    ToggleHotkeyPressed?.Invoke();
                }
            }
        }

        // Check save hotkey
        if (_saveVk != 0 && vkCode == _saveVk)
        {
            bool modMatch = ModifiersMatch(_saveMod);
            Log($"Save key matched vk=0x{vkCode:X2}, modMatch={modMatch}");
            if (modMatch)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastSave).TotalSeconds >= Constants.HotkeyDebounceS)
                {
                    _lastSave = now;
                    Log("SAVE HOTKEY FIRED");
                    SaveHotkeyPressed?.Invoke();
                }
            }
        }
    }

    private static bool ModifiersMatch(uint requiredMod)
    {
        bool ctrlRequired = (requiredMod & NativeMethods.MOD_CONTROL) != 0;
        bool altRequired = (requiredMod & NativeMethods.MOD_ALT) != 0;
        bool shiftRequired = (requiredMod & NativeMethods.MOD_SHIFT) != 0;
        bool winRequired = (requiredMod & NativeMethods.MOD_WIN) != 0;

        bool ctrlDown = IsKeyDown(NativeMethods.VK_CONTROL);
        bool altDown = IsKeyDown(NativeMethods.VK_MENU);
        bool shiftDown = IsKeyDown(NativeMethods.VK_SHIFT);
        bool winDown = IsKeyDown(NativeMethods.VK_LWIN) || IsKeyDown(NativeMethods.VK_RWIN);

        return ctrlRequired == ctrlDown &&
               altRequired == altDown &&
               shiftRequired == shiftDown &&
               winRequired == winDown;
    }

    private static bool IsKeyDown(int vk)
    {
        return (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    // --- Parsing (unchanged) ---

    internal static bool ParseHotkey(string hotkeyStr, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyStr) || hotkeyStr.Length < 2)
            return false;

        var normalized = hotkeyStr.ToLowerInvariant();
        normalized = normalized.Replace("num+", "num add")
                               .Replace("num-", "num subtract")
                               .Replace("num*", "num multiply")
                               .Replace("num/", "num divide");

        var parts = normalized.Split('+');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            switch (trimmed)
            {
                case "ctrl" or "control":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "shift":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "win":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    vk = KeyNameToVk(trimmed);
                    break;
            }
        }

        return vk != 0;
    }

    private static uint KeyNameToVk(string name)
    {
        if (name.Length >= 2 && name[0] == 'f' && int.TryParse(name[1..], out int fNum) && fNum is >= 1 and <= 24)
            return (uint)(0x70 + fNum - 1);

        return name switch
        {
            "num add" or "numpadadd" or "num+" => 0x6B,
            "num subtract" or "numpadsubtract" or "num-" => 0x6D,
            "num multiply" or "numpadmultiply" or "num*" => 0x6A,
            "num divide" or "numpaddivide" or "num/" => 0x6F,
            "num 0" or "numpad0" => 0x60,
            "num 1" or "numpad1" => 0x61,
            "num 2" or "numpad2" => 0x62,
            "num 3" or "numpad3" => 0x63,
            "num 4" or "numpad4" => 0x64,
            "num 5" or "numpad5" => 0x65,
            "num 6" or "numpad6" => 0x66,
            "num 7" or "numpad7" => 0x67,
            "num 8" or "numpad8" => 0x68,
            "num 9" or "numpad9" => 0x69,
            "numpadenter" or "num enter" => 0x0D,
            "space" => 0x20,
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "escape" or "esc" => 0x1B,
            "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            "insert" or "ins" => 0x2D,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" or "page up" => 0x21,
            "pagedown" or "page down" => 0x22,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            "printscreen" or "print screen" or "prtsc" => 0x2C,
            "scrolllock" or "scroll lock" => 0x91,
            "pause" or "break" => 0x13,
            "capslock" or "caps lock" => 0x14,
            "numlock" or "num lock" => 0x90,
            _ when name.Length == 1 && char.IsLetterOrDigit(name[0]) =>
                (uint)char.ToUpperInvariant(name[0]),
            _ => 0
        };
    }

    internal static string FormatHotkeyDisplay(string hotkeyStr)
    {
        if (string.IsNullOrWhiteSpace(hotkeyStr))
            return "";

        var normalized = hotkeyStr.Replace("num+", "num add")
                                  .Replace("num-", "num subtract")
                                  .Replace("num*", "num multiply")
                                  .Replace("num/", "num divide");

        var parts = normalized.Split('+');
        var formatted = new List<string>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim().ToLowerInvariant();
            formatted.Add(trimmed switch
            {
                "ctrl" or "control" => "Ctrl",
                "alt" => "Alt",
                "shift" => "Shift",
                "win" => "Win",
                "num add" or "numpadadd" => "NUM +",
                "num subtract" or "numpadsubtract" => "NUM -",
                "num multiply" or "numpadmultiply" => "NUM *",
                "num divide" or "numpaddivide" => "NUM /",
                "num enter" or "numpadenter" => "NUM Enter",
                "num 0" or "numpad0" => "NUM 0",
                "num 1" or "numpad1" => "NUM 1",
                "num 2" or "numpad2" => "NUM 2",
                "num 3" or "numpad3" => "NUM 3",
                "num 4" or "numpad4" => "NUM 4",
                "num 5" or "numpad5" => "NUM 5",
                "num 6" or "numpad6" => "NUM 6",
                "num 7" or "numpad7" => "NUM 7",
                "num 8" or "numpad8" => "NUM 8",
                "num 9" or "numpad9" => "NUM 9",
                "space" => "Space",
                "enter" or "return" => "Enter",
                "tab" => "Tab",
                "escape" or "esc" => "Esc",
                "backspace" => "Backspace",
                "delete" or "del" => "Delete",
                "insert" or "ins" => "Insert",
                "home" => "Home",
                "end" => "End",
                "pageup" or "page up" => "Page Up",
                "pagedown" or "page down" => "Page Down",
                "up" => "Up",
                "down" => "Down",
                "left" => "Left",
                "right" => "Right",
                "printscreen" or "print screen" or "prtsc" => "Print Screen",
                "scrolllock" or "scroll lock" => "Scroll Lock",
                "pause" or "break" => "Pause",
                "capslock" or "caps lock" => "Caps Lock",
                "numlock" or "num lock" => "Num Lock",
                _ when trimmed.Length >= 2 && trimmed[0] == 'f' &&
                       int.TryParse(trimmed[1..], out _) => trimmed.ToUpperInvariant(),
                _ when trimmed.Length == 1 => trimmed.ToUpperInvariant(),
                _ => trimmed
            });
        }

        return string.Join("+", formatted);
    }

    internal static string ParseHotkeyDisplay(string displayStr)
    {
        if (string.IsNullOrWhiteSpace(displayStr))
            return "";

        var safe = displayStr
            .Replace("NUM +", "\x01NUMADD")
            .Replace("NUM -", "\x01NUMSUB")
            .Replace("NUM *", "\x01NUMMUL")
            .Replace("NUM /", "\x01NUMDIV");

        var parts = safe.Split('+');
        var parsed = new List<string>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            parsed.Add(trimmed switch
            {
                "\x01NUMADD" => "num add",
                "\x01NUMSUB" => "num subtract",
                "\x01NUMMUL" => "num multiply",
                "\x01NUMDIV" => "num divide",
                "NUM Enter" => "num enter",
                "NUM 0" => "num 0",
                "NUM 1" => "num 1",
                "NUM 2" => "num 2",
                "NUM 3" => "num 3",
                "NUM 4" => "num 4",
                "NUM 5" => "num 5",
                "NUM 6" => "num 6",
                "NUM 7" => "num 7",
                "NUM 8" => "num 8",
                "NUM 9" => "num 9",
                _ => trimmed.ToLowerInvariant()
            });
        }

        return string.Join("+", parsed);
    }

    // --- Diagnostics ---

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReplayOverlay", "hotkey.log");

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { /* best-effort */ }
    }

    public void Dispose()
    {
        Unregister();
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _hookProc = null;
        GC.SuppressFinalize(this);
    }
}
