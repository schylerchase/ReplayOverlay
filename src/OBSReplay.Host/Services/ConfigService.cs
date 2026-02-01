using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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

    private static readonly string DefaultWatchFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

    private const string DefaultObsPath = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";

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
            var config = loaded ?? new AppConfig();

            config.ObsPassword = DecryptPassword(config.ObsPassword);
            ValidatePaths(config);

            return config;
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

            // Encrypt password for disk storage without mutating the caller's object
            var plaintextPassword = config.ObsPassword;
            config.ObsPassword = EncryptPassword(plaintextPassword);

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
            SetFilePermissions(ConfigPath);

            // Restore plaintext so the in-memory config remains usable
            config.ObsPassword = plaintextPassword;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    // --- DPAPI password encryption ---

    private const string EncryptedPrefix = "dpapi:";

    private static string EncryptPassword(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        try
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptedBytes = DpapiProtect(plaintextBytes);
            return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Password encryption failed, storing as-is: {ex.Message}");
            return plaintext;
        }
    }

    private static string DecryptPassword(string stored)
    {
        if (string.IsNullOrEmpty(stored))
            return stored;

        // Already encrypted with our prefix -- decrypt it
        if (stored.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            try
            {
                var base64 = stored[EncryptedPrefix.Length..];
                var encryptedBytes = Convert.FromBase64String(base64);
                var plaintextBytes = DpapiUnprotect(encryptedBytes);
                return Encoding.UTF8.GetString(plaintextBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Password decryption failed: {ex.Message}");
                return "";
            }
        }

        // No prefix -- this is a plaintext password from a pre-encryption config.
        // Return as-is; it will be encrypted on the next Save().
        return stored;
    }

    /// <summary>
    /// Encrypts data using Windows DPAPI (CurrentUser scope) via crypt32.dll.
    /// </summary>
    private static byte[] DpapiProtect(byte[] plaintext)
    {
        var dataIn = new NativeMethods.DATA_BLOB
        {
            cbData = plaintext.Length,
            pbData = Marshal.AllocHGlobal(plaintext.Length)
        };

        try
        {
            Marshal.Copy(plaintext, 0, dataIn.pbData, plaintext.Length);

            if (!NativeMethods.CryptProtectData(
                    ref dataIn, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.CRYPTPROTECT_UI_FORBIDDEN, out var dataOut))
            {
                throw new InvalidOperationException(
                    $"CryptProtectData failed (error {Marshal.GetLastWin32Error()})");
            }

            try
            {
                var result = new byte[dataOut.cbData];
                Marshal.Copy(dataOut.pbData, result, 0, dataOut.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(dataOut.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(dataIn.pbData);
        }
    }

    /// <summary>
    /// Decrypts data using Windows DPAPI (CurrentUser scope) via crypt32.dll.
    /// </summary>
    private static byte[] DpapiUnprotect(byte[] encrypted)
    {
        var dataIn = new NativeMethods.DATA_BLOB
        {
            cbData = encrypted.Length,
            pbData = Marshal.AllocHGlobal(encrypted.Length)
        };

        try
        {
            Marshal.Copy(encrypted, 0, dataIn.pbData, encrypted.Length);

            if (!NativeMethods.CryptUnprotectData(
                    ref dataIn, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.CRYPTPROTECT_UI_FORBIDDEN, out var dataOut))
            {
                throw new InvalidOperationException(
                    $"CryptUnprotectData failed (error {Marshal.GetLastWin32Error()})");
            }

            try
            {
                var result = new byte[dataOut.cbData];
                Marshal.Copy(dataOut.pbData, result, 0, dataOut.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(dataOut.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(dataIn.pbData);
        }
    }

    // --- Path validation ---

    private static void ValidatePaths(AppConfig config)
    {
        config.WatchFolder = ValidatePath(config.WatchFolder, DefaultWatchFolder, "WatchFolder");
        config.ObsPath = ValidatePath(config.ObsPath, DefaultObsPath, "ObsPath");
    }

    private static string ValidatePath(string path, string fallback, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.WriteLine($"Config path '{fieldName}' is empty, using default.");
            return fallback;
        }

        // Reject path traversal sequences
        if (path.Contains("..", StringComparison.Ordinal))
        {
            Debug.WriteLine($"Config path '{fieldName}' contains '..' traversal sequence, using default.");
            return fallback;
        }

        // Require fully qualified (absolute) path
        if (!Path.IsPathFullyQualified(path))
        {
            Debug.WriteLine($"Config path '{fieldName}' is not fully qualified, using default.");
            return fallback;
        }

        return path;
    }

    // --- File permissions ---

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
        catch (Exception ex)
        {
            // Non-critical: permission setting is best-effort security hardening
            Debug.WriteLine($"SetFilePermissions failed (non-critical): {ex.Message}");
        }
    }
}
