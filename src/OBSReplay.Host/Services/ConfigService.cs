using System.Diagnostics;
using System.IO;
using System.Text.Json;
using OBSReplay.Host.Models;

namespace OBSReplay.Host.Services;

public class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReplayOverlay");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly string LogPath = Path.Combine(ConfigDir, "overlay.log");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string GetLogPath() => LogPath;

    public bool IsFirstRun() => !File.Exists(ConfigPath);

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return loaded ?? new AppConfig();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load config: {ex.Message}");
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
            SetFilePermissions(ConfigPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    private static void SetFilePermissions(string path)
    {
        try
        {
            var username = Environment.UserName;
            var startInfo = new ProcessStartInfo
            {
                FileName = "icacls",
                Arguments = $"\"{path}\" /inheritance:r /grant:r \"{username}:(R,W)\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch
        {
            // Non-critical: permission setting is best-effort security hardening
        }
    }
}
