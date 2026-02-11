using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ReplayOverlay.Host.Services;

public static class AdminService
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ReplayOverlay";

    public static bool IsAdmin()
    {
        try { return NativeMethods.IsUserAnAdmin(); }
        catch (Exception ex)
        {
            Debug.WriteLine($"IsAdmin check failed: {ex.Message}");
            return false;
        }
    }

    public static bool RequestElevation()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe))
                return false;

            var args = string.Join(" ", Environment.GetCommandLineArgs()
                .Skip(1)
                .Select(a => $"\"{a}\""));

            int result = NativeMethods.ShellExecuteW(
                IntPtr.Zero,
                "runas",
                exe,
                string.IsNullOrEmpty(args) ? null : args,
                null,
                NativeMethods.SW_SHOWNORMAL);

            return result > 32;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Elevation request failed: {ex.Message}");
            return false;
        }
    }

    public static bool SetWindowsStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
            if (key == null) return false;

            if (enabled)
            {
                var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return false;
                key.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                try { key.DeleteValue(AppName); }
                catch (ArgumentException) { /* doesn't exist */ }
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup registry error: {ex.Message}");
            return false;
        }
    }

    public static bool GetWindowsStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetWindowsStartup registry read failed: {ex.Message}");
            return false;
        }
    }

    public static bool IsValidObsExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        var fileName = Path.GetFileName(path).ToLowerInvariant();
        if (fileName is not ("obs64.exe" or "obs32.exe"))
            return false;

        var fullPath = Path.GetFullPath(path).ToLowerInvariant();
        return fullPath.Contains("obs-studio") || fullPath.Contains("obs studio");
    }
}
