namespace ReplayOverlay.Host.Models;

public static class Constants
{
    // Polling intervals
    public const int PreviewIntervalMs = 250;
    public const int StatusIntervalMs = 1000;
    public const int BufferMonitorIntervalMs = 1000;

    // Preview dimensions
    public const int PreviewWidth = 320;
    public const int PreviewHeight = 180;
    public const int DisplayPreviewWidth = 178;
    public const int DisplayPreviewHeight = 100;
    public const int MaxScreenshotBytes = 10 * 1024 * 1024;

    // File organization
    public const double FilePollIntervalS = 0.5;
    public const int FileStableChecks = 3;
    public const double FileCompletionTimeoutS = 30.0;
    public const int RecentFileCleanupS = 10;

    // Debounce
    public const double AudioDebounceS = 1.5;
    public const double HotkeyDebounceS = 2.0;
    public const int ButtonDebounceMs = 2000;

    // OBS connection
    public const int ObsConnectRetries = 10;
    public const double ObsConnectDelayS = 2.0;
    public const double SceneCacheTtlS = 5.0;
    public const double AudioListCacheTtlS = 10.0;

    // OBS audio fader curve
    public const double FaderLogRangeDb = -96.0;
    public const double FaderLogOffsetDb = 6.0;

    // IPC
    public const string PipeName = "ReplayOverlayPipe";
    public const string OverlayExeName = "OverlayRenderer.exe";

    // REC indicator blink
    public const int RecBlinkIntervalMs = 500;

    // Video file extensions for replay detection
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".flv", ".avi", ".mov", ".ts", ".m4v"
    };

    // Processes to ignore when detecting foreground game
    public static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "searchhost", "shellexperiencehost", "applicationframehost",
        "systemsettings", "textinputhost", "dwm", "csrss", "winlogon",
        "chrome", "firefox", "msedge", "opera", "brave",
        "discord", "slack", "teams", "zoom", "spotify",
        "code", "devenv", "obs64", "obs32", "obs"
    };

    // Theme colors
    public static class Colors
    {
        public const string Background = "#1a1a2e";
        public const string Secondary = "#16213e";
        public const string Border = "#2c3e50";
        public const string Text = "#eaeaea";
        public const string TextSecondary = "#7f8c8d";
        public const string Accent = "#4ecca3";
        public const string Alert = "#e94560";
        public const string Warning = "#f39c12";
    }
}
