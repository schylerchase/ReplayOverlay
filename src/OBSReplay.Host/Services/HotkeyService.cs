using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OBSReplay.Host.Models;

namespace OBSReplay.Host.Services;

public class HotkeyService : IDisposable
{
    private const int ToggleHotkeyId = 9001;
    private const int SaveHotkeyId = 9002;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _registered;

    // Debounce - toggle only needs a short guard (MOD_NOREPEAT handles key repeat),
    // save needs longer to prevent accidental double-saves
    private const double ToggleDebounceS = 0.3;
    private DateTime _lastToggle = DateTime.MinValue;
    private DateTime _lastSave = DateTime.MinValue;
    private readonly object _lock = new();

    public event Action? ToggleHotkeyPressed;
    public event Action? SaveHotkeyPressed;

    public void Initialize()
    {
        // Create a hidden message-only window for WM_HOTKEY
        var parameters = new HwndSourceParameters("OBSReplayHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0, // Not visible
        };
        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
        _hwnd = _hwndSource.Handle;
    }

    public void Register(string toggleKey, string saveKey)
    {
        Unregister();

        if (ParseHotkey(toggleKey, out uint toggleMod, out uint toggleVk))
        {
            NativeMethods.RegisterHotKey(_hwnd, ToggleHotkeyId,
                toggleMod | NativeMethods.MOD_NOREPEAT, toggleVk);
        }

        if (ParseHotkey(saveKey, out uint saveMod, out uint saveVk))
        {
            NativeMethods.RegisterHotKey(_hwnd, SaveHotkeyId,
                saveMod | NativeMethods.MOD_NOREPEAT, saveVk);
        }

        _registered = true;
        Debug.WriteLine($"Hotkeys registered: toggle={toggleKey}, save={saveKey}");
    }

    public void Unregister()
    {
        if (!_registered || _hwnd == IntPtr.Zero) return;

        NativeMethods.UnregisterHotKey(_hwnd, ToggleHotkeyId);
        NativeMethods.UnregisterHotKey(_hwnd, SaveHotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                if (id == ToggleHotkeyId &&
                    (now - _lastToggle).TotalSeconds >= ToggleDebounceS)
                {
                    _lastToggle = now;
                    ToggleHotkeyPressed?.Invoke();
                }
                else if (id == SaveHotkeyId &&
                         (now - _lastSave).TotalSeconds >= Constants.HotkeyDebounceS)
                {
                    _lastSave = now;
                    SaveHotkeyPressed?.Invoke();
                }
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Parses a hotkey string like "f10", "ctrl+shift+s", "num add" into
    /// Win32 modifier flags and virtual key code.
    /// </summary>
    internal static bool ParseHotkey(string hotkeyStr, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyStr) || hotkeyStr.Length < 2)
            return false;

        var parts = hotkeyStr.ToLowerInvariant().Split('+');

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
        // Function keys
        if (name.Length >= 2 && name[0] == 'f' && int.TryParse(name[1..], out int fNum) && fNum is >= 1 and <= 24)
            return (uint)(0x70 + fNum - 1); // VK_F1 = 0x70

        // Numpad keys
        return name switch
        {
            "num add" or "numpadadd" => 0x6B,       // VK_ADD
            "num subtract" or "numpadsubtract" => 0x6D, // VK_SUBTRACT
            "num multiply" or "numpadmultiply" => 0x6A, // VK_MULTIPLY
            "num divide" or "numpaddivide" => 0x6F,     // VK_DIVIDE
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
            "numpadenter" or "num enter" => 0x0D, // VK_RETURN (same as Enter)

            // Common keys
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

            // Single letter/digit
            _ when name.Length == 1 && char.IsLetterOrDigit(name[0]) =>
                (uint)char.ToUpperInvariant(name[0]),

            _ => 0
        };
    }

    /// <summary>
    /// Converts internal hotkey format to user-friendly display.
    /// "num add" -> "NUM +", "f10" -> "F10", "ctrl+shift+s" -> "Ctrl+Shift+S"
    /// </summary>
    internal static string FormatHotkeyDisplay(string hotkeyStr)
    {
        if (string.IsNullOrWhiteSpace(hotkeyStr))
            return "";

        var parts = hotkeyStr.Split('+');
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

    /// <summary>
    /// Converts user-friendly display format back to internal format.
    /// "NUM +" -> "num add", "F10" -> "f10", "Ctrl+Shift+S" -> "ctrl+shift+s"
    /// </summary>
    internal static string ParseHotkeyDisplay(string displayStr)
    {
        if (string.IsNullOrWhiteSpace(displayStr))
            return "";

        var parts = displayStr.Split('+');
        var parsed = new List<string>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            parsed.Add(trimmed switch
            {
                "NUM +" => "num add",
                "NUM -" => "num subtract",
                "NUM *" => "num multiply",
                "NUM /" => "num divide",
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

    public void Dispose()
    {
        Unregister();
        _hwndSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
