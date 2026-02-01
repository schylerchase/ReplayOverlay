using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OBSReplay.Host.Services;

public static class ObsHotkeySyncService
{
    /// <summary>
    /// Reads the OBS replay buffer save hotkey from OBS Studio's configuration files.
    /// Returns the hotkey string (e.g., "f9", "num add") or null if not found.
    /// </summary>
    public static string? ReadObsReplayHotkey()
    {
        try
        {
            var obsConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "obs-studio");

            // Read global.ini to find the current profile name
            var globalIni = Path.Combine(obsConfigDir, "global.ini");
            if (!File.Exists(globalIni))
                return null;

            var profileName = ReadIniValue(globalIni, "BasicWindow", "Profile");
            if (string.IsNullOrEmpty(profileName))
                profileName = "Untitled";

            // Sanitize profile name to prevent path traversal
            profileName = SanitizeProfileName(profileName);

            // Read basic.ini from the profile directory
            var basicIni = Path.Combine(obsConfigDir, "basic", "profiles", profileName, "basic.ini");
            if (!File.Exists(basicIni))
                return null;

            // OBS stores hotkeys in the [Hotkeys] section of basic.ini.
            // Format: ReplayBuffer={"ReplayBuffer.Save":[{"key":"OBS_KEY_NUMPLUS"}]}
            // The INI key is "ReplayBuffer" and the value is a JSON object.
            var replayBufferJson = ReadIniValue(basicIni, "Hotkeys", "ReplayBuffer");
            if (!string.IsNullOrEmpty(replayBufferJson))
            {
                var hotkey = ExtractHotkeyFromJson(replayBufferJson, "ReplayBuffer.Save");
                if (hotkey != null)
                    return hotkey;
            }

            // Fallback: try reading the entire file for alternate formats used by older OBS
            var content = File.ReadAllText(basicIni);

            // Try: "ReplayBuffer.Save" : [{...}] (standalone JSON key)
            var match = Regex.Match(content, @"""ReplayBuffer\.Save""\s*:\s*(\[[^\]]*\])",
                RegexOptions.Singleline);
            if (match.Success)
            {
                var hotkey = ExtractFirstKeyFromArray(match.Groups[1].Value);
                if (hotkey != null)
                    return hotkey;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OBS hotkey sync failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Reads the OBS recording directory from OBS Studio's configuration files.
    /// Checks AdvOut.RecFilePath or SimpleOutput.FilePath depending on output mode.
    /// Returns null if not found.
    /// </summary>
    public static string? ReadObsRecordDirectory()
    {
        try
        {
            var obsConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "obs-studio");

            var globalIni = Path.Combine(obsConfigDir, "global.ini");
            if (!File.Exists(globalIni))
                return null;

            var profileName = ReadIniValue(globalIni, "BasicWindow", "Profile");
            if (string.IsNullOrEmpty(profileName))
                profileName = "Untitled";

            profileName = SanitizeProfileName(profileName);

            var basicIni = Path.Combine(obsConfigDir, "basic", "profiles", profileName, "basic.ini");
            if (!File.Exists(basicIni))
                return null;

            // Check output mode
            var mode = ReadIniValue(basicIni, "Output", "Mode") ?? "Simple";

            string? dir = null;
            if (mode.Equals("Advanced", StringComparison.OrdinalIgnoreCase))
                dir = ReadIniValue(basicIni, "AdvOut", "RecFilePath");
            else
                dir = ReadIniValue(basicIni, "SimpleOutput", "FilePath");

            if (!string.IsNullOrEmpty(dir))
            {
                // OBS may use double backslashes in config
                dir = dir.Replace("\\\\", "\\");
                if (Directory.Exists(dir))
                    return dir;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OBS record directory sync failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Extracts a hotkey from a JSON object like {"ReplayBuffer.Save":[{"key":"OBS_KEY_NUMPLUS"}]}
    /// </summary>
    internal static string? ExtractHotkeyFromJson(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var arrayProp) &&
                arrayProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in arrayProp.EnumerateArray())
                {
                    if (element.TryGetProperty("key", out var keyProp))
                    {
                        var obsKeyName = keyProp.GetString();
                        if (!string.IsNullOrEmpty(obsKeyName))
                        {
                            var mapped = MapObsKeyName(obsKeyName);
                            if (mapped != null)
                                return mapped;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ExtractHotkeyFromJson failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Extracts the first key from a JSON array like [{"key":"OBS_KEY_F9"}]
    /// </summary>
    private static string? ExtractFirstKeyFromArray(string jsonArray)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonArray);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("key", out var keyProp))
                {
                    var obsKeyName = keyProp.GetString();
                    if (!string.IsNullOrEmpty(obsKeyName))
                    {
                        var mapped = MapObsKeyName(obsKeyName);
                        if (mapped != null)
                            return mapped;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ExtractFirstKeyFromArray failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Maps OBS key names (e.g., "OBS_KEY_NUMPADADD") to keyboard-library
    /// format (e.g., "num add").
    /// </summary>
    internal static string? MapObsKeyName(string obsKey)
    {
        // Strip OBS_KEY_ prefix
        var key = obsKey;
        if (key.StartsWith("OBS_KEY_"))
            key = key[8..];

        return key.ToUpperInvariant() switch
        {
            // Function keys
            "F1" => "f1", "F2" => "f2", "F3" => "f3", "F4" => "f4",
            "F5" => "f5", "F6" => "f6", "F7" => "f7", "F8" => "f8",
            "F9" => "f9", "F10" => "f10", "F11" => "f11", "F12" => "f12",
            "F13" => "f13", "F14" => "f14", "F15" => "f15", "F16" => "f16",

            // Numpad
            "NUMPLUS" or "NUMPADADD" or "NUMPAD_ADD" => "num add",
            "NUMMINUS" or "NUMPADSUBTRACT" or "NUMPAD_SUBTRACT" => "num subtract",
            "NUMASTERISK" or "NUMPADMULTIPLY" or "NUMPAD_MULTIPLY" => "num multiply",
            "NUMSLASH" or "NUMPADDIVIDE" or "NUMPAD_DIVIDE" => "num divide",
            "NUMPERIOD" => "num decimal",
            "NUMPADENTER" or "NUMPAD_ENTER" => "num enter",
            "NUMPAD0" => "num 0", "NUMPAD1" => "num 1",
            "NUMPAD2" => "num 2", "NUMPAD3" => "num 3",
            "NUMPAD4" => "num 4", "NUMPAD5" => "num 5",
            "NUMPAD6" => "num 6", "NUMPAD7" => "num 7",
            "NUMPAD8" => "num 8", "NUMPAD9" => "num 9",

            // Common keys
            "SPACE" => "space",
            "RETURN" or "ENTER" => "enter",
            "ESCAPE" => "escape",
            "TAB" => "tab",
            "BACKSPACE" => "backspace",
            "DELETE" => "delete",
            "INSERT" => "insert",
            "HOME" => "home",
            "END" => "end",
            "PAGEUP" => "pageup",
            "PAGEDOWN" => "pagedown",
            "UP" => "up", "DOWN" => "down", "LEFT" => "left", "RIGHT" => "right",
            "PRINT" => "printscreen",
            "SCROLLLOCK" => "scrolllock",
            "PAUSE" => "pause",

            // Single letters
            _ when key.Length == 1 && char.IsLetterOrDigit(key[0]) => key.ToLowerInvariant(),

            _ => null
        };
    }

    private static string? ReadIniValue(string path, string section, string key)
    {
        bool inSection = false;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSection = line[1..^1] == section;
                continue;
            }

            if (inSection && line.StartsWith(key + "="))
            {
                return line[(key.Length + 1)..].Trim();
            }
        }
        return null;
    }

    private static string SanitizeProfileName(string name)
    {
        // Remove path traversal attempts and invalid chars
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return sanitized.Replace("..", "").Trim();
    }
}
