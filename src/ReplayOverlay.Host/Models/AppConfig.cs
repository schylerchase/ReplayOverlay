using System.IO;
using System.Text.Json.Serialization;

namespace ReplayOverlay.Host.Models;

public class AppConfig
{
    [JsonPropertyName("obs_port")]
    public int ObsPort { get; set; } = 4455;

    [JsonPropertyName("obs_password")]
    public string ObsPassword { get; set; } = "";

    [JsonPropertyName("toggle_hotkey")]
    public string ToggleHotkey { get; set; } = "f10";

    [JsonPropertyName("save_hotkey")]
    public string SaveHotkey { get; set; } = "f9";

    [JsonPropertyName("hotkey_enabled")]
    public bool HotkeyEnabled { get; set; } = true;

    [JsonPropertyName("watch_folder")]
    public string WatchFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

    [JsonPropertyName("organize_by_game")]
    public bool OrganizeByGame { get; set; } = true;

    [JsonPropertyName("sync_obs_folder")]
    public bool SyncObsFolder { get; set; } = true;

    [JsonPropertyName("show_notifications")]
    public bool ShowNotifications { get; set; } = true;

    [JsonPropertyName("notification_duration")]
    public double NotificationDuration { get; set; } = 3.0;

    [JsonPropertyName("notification_opacity")]
    public int NotificationOpacity { get; set; } = 25;

    [JsonPropertyName("notification_message")]
    public string NotificationMessage { get; set; } = "REPLAY SAVED";

    [JsonPropertyName("show_rec_indicator")]
    public bool ShowRecIndicator { get; set; } = true;

    [JsonPropertyName("rec_indicator_position")]
    public string RecIndicatorPosition { get; set; } = "top-left";

    [JsonPropertyName("auto_launch_obs")]
    public bool AutoLaunchObs { get; set; } = false;

    [JsonPropertyName("auto_launch_minimized")]
    public bool AutoLaunchMinimized { get; set; } = true;

    [JsonPropertyName("auto_start_buffer")]
    public bool AutoStartBuffer { get; set; } = false;

    [JsonPropertyName("obs_path")]
    public string ObsPath { get; set; } = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";

    [JsonPropertyName("run_as_admin")]
    public bool RunAsAdmin { get; set; } = false;

    [JsonPropertyName("start_with_windows")]
    public bool StartWithWindows { get; set; } = false;

    [JsonPropertyName("overlay_x")]
    public int? OverlayX { get; set; }

    [JsonPropertyName("overlay_y")]
    public int? OverlayY { get; set; }
}
