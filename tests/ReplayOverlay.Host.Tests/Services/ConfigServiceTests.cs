using System.IO;
using System.Text.Json;
using ReplayOverlay.Host.Models;
using ReplayOverlay.Host.Services;
using Xunit;

namespace ReplayOverlay.Host.Tests.Services;

public class ConfigServiceTests
{
    [Fact]
    public void DefaultAppConfig_HasCorrectValues()
    {
        // Test default construction (not file-based, to isolate from real config)
        var config = new AppConfig();

        Assert.Equal(4455, config.ObsPort);
        Assert.Equal("", config.ObsPassword);
        Assert.Equal("f10", config.ToggleHotkey);
        Assert.Equal("f9", config.SaveHotkey);
        Assert.True(config.HotkeyEnabled);
        Assert.True(config.OrganizeByGame);
        Assert.True(config.SyncObsFolder);
        Assert.True(config.ShowNotifications);
        Assert.Equal(3.0, config.NotificationDuration);
        Assert.Equal(25, config.NotificationOpacity);
        Assert.Equal("REPLAY SAVED", config.NotificationMessage);
        Assert.True(config.ShowRecIndicator);
        Assert.Equal("top-left", config.RecIndicatorPosition);
        Assert.False(config.AutoLaunchObs);
        Assert.True(config.AutoLaunchMinimized);
        Assert.False(config.AutoStartBuffer);
        Assert.False(config.RunAsAdmin);
        Assert.False(config.StartWithWindows);
        Assert.Null(config.OverlayX);
        Assert.Null(config.OverlayY);
    }

    [Fact]
    public void AppConfig_SerializationRoundTrip()
    {
        var original = new AppConfig
        {
            ObsPort = 5555,
            ObsPassword = "secret",
            ToggleHotkey = "F12",
            OverlayX = 100,
            OverlayY = 200,
            OrganizeByGame = false,
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(5555, deserialized!.ObsPort);
        Assert.Equal("secret", deserialized.ObsPassword);
        Assert.Equal("F12", deserialized.ToggleHotkey);
        Assert.Equal(100, deserialized.OverlayX);
        Assert.Equal(200, deserialized.OverlayY);
        Assert.False(deserialized.OrganizeByGame);
    }

    [Fact]
    public void AppConfig_NullOverlayPositionSurvivesRoundTrip()
    {
        var config = new AppConfig(); // OverlayX/Y default to null
        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.OverlayX);
        Assert.Null(deserialized.OverlayY);
    }

    [Fact]
    public void AppConfig_JsonPropertyNamesMatchPython()
    {
        var config = new AppConfig { ObsPort = 4455 };
        var json = JsonSerializer.Serialize(config);

        // Verify the JSON uses snake_case property names matching the Python config
        Assert.Contains("\"obs_port\"", json);
        Assert.Contains("\"obs_password\"", json);
        Assert.Contains("\"toggle_hotkey\"", json);
        Assert.Contains("\"save_hotkey\"", json);
        Assert.Contains("\"watch_folder\"", json);
        Assert.Contains("\"organize_by_game\"", json);
        Assert.Contains("\"auto_launch_obs\"", json);
        Assert.Contains("\"overlay_x\"", json);
    }
}
