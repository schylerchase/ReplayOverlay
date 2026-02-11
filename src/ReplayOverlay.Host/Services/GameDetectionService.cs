using System.Diagnostics;
using System.IO;
using ReplayOverlay.Host.Models;

namespace ReplayOverlay.Host.Services;

public class GameDetectionService
{
    private string? _lastGame;

    public string? LastGame => _lastGame;

    /// <summary>
    /// Captures the foreground process name. If it's a game (not in the ignore list),
    /// stores it as the last known game.
    /// </summary>
    public string? CaptureCurrentGame()
    {
        var processName = GetForegroundProcessName();
        if (processName != null && !IsIgnored(processName))
        {
            _lastGame = processName;
            return processName;
        }
        return null;
    }

    /// <summary>
    /// Determines the game folder name for organizing a replay.
    /// Returns null if organize_by_game is disabled.
    /// </summary>
    public string? PrepareGameFolder(AppConfig config)
    {
        if (!config.OrganizeByGame)
            return null;

        // Try current foreground first
        var current = GetForegroundProcessName();
        if (current != null && !IsIgnored(current))
        {
            _lastGame = current;
            return current;
        }

        // Fall back to last known game
        if (!string.IsNullOrEmpty(_lastGame))
            return _lastGame;

        // Final fallback
        return "Desktop";
    }

    public static bool IsIgnored(string processName)
    {
        return Constants.IgnoredProcesses.Contains(processName);
    }

    /// <summary>
    /// Gets the process name of the foreground window's owning process.
    /// Uses Win32 API: GetForegroundWindow -> GetWindowThreadProcessId -> OpenProcess -> QueryFullProcessImageNameW
    /// </summary>
    public static string? GetForegroundProcessName()
    {
        try
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
                return null;

            IntPtr hProcess = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProcess == IntPtr.Zero)
                return null;

            try
            {
                var buffer = new char[1024];
                uint size = (uint)buffer.Length;
                if (NativeMethods.QueryFullProcessImageNameW(hProcess, 0, buffer, ref size))
                {
                    var fullPath = new string(buffer, 0, (int)size);
                    return Path.GetFileNameWithoutExtension(fullPath);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Game detection failed: {ex.Message}");
        }

        return null;
    }
}
