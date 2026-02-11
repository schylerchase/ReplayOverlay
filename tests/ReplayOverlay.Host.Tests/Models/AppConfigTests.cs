using System.Text.Json;
using ReplayOverlay.Host.Models;
using Xunit;

namespace ReplayOverlay.Host.Tests.Models;

public class AppConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedDefaults()
    {
        var config = new AppConfig();
        Assert.Equal(4455, config.ObsPort);
        Assert.Equal("", config.ObsPassword);
        Assert.False(config.AutoStartBuffer);
        Assert.True(config.ShowNotifications);
        Assert.Equal("REPLAY SAVED", config.NotificationMessage);
        Assert.Equal(3.0, config.NotificationDuration);
        Assert.True(config.HotkeyEnabled);
        Assert.Equal("f10", config.ToggleHotkey);
        Assert.Equal("f9", config.SaveHotkey);
        Assert.True(config.OrganizeByGame);
        Assert.False(config.StartWithWindows);
        Assert.False(config.AutoLaunchObs);
    }

    [Fact]
    public void Serialization_UsesSnakeCasePropertyNames()
    {
        var config = new AppConfig();
        var json = JsonSerializer.Serialize(config);

        Assert.Contains("\"obs_port\"", json);
        Assert.Contains("\"obs_password\"", json);
        Assert.Contains("\"auto_start_buffer\"", json);
        Assert.Contains("\"show_notifications\"", json);
        Assert.Contains("\"notification_message\"", json);
        Assert.Contains("\"notification_duration\"", json);
        Assert.Contains("\"hotkey_enabled\"", json);
        Assert.Contains("\"toggle_hotkey\"", json);
        Assert.Contains("\"save_hotkey\"", json);
        Assert.Contains("\"organize_by_game\"", json);
        Assert.Contains("\"start_with_windows\"", json);
        Assert.Contains("\"auto_launch_obs\"", json);
        Assert.Contains("\"watch_folder\"", json);
    }

    [Fact]
    public void Deserialization_FromSnakeCaseJson()
    {
        var json = """
        {
            "obs_port": 5555,
            "obs_password": "secret",
            "toggle_hotkey": "f5",
            "save_hotkey": "num add",
            "organize_by_game": false,
            "notification_message": "SAVED!"
        }
        """;

        var config = JsonSerializer.Deserialize<AppConfig>(json);
        Assert.NotNull(config);
        Assert.Equal(5555, config.ObsPort);
        Assert.Equal("secret", config.ObsPassword);
        Assert.Equal("f5", config.ToggleHotkey);
        Assert.Equal("num add", config.SaveHotkey);
        Assert.False(config.OrganizeByGame);
        Assert.Equal("SAVED!", config.NotificationMessage);
    }

    [Fact]
    public void Deserialization_MissingFieldsGetDefaults()
    {
        var json = """{ "obs_port": 9999 }""";
        var config = JsonSerializer.Deserialize<AppConfig>(json);
        Assert.NotNull(config);
        Assert.Equal(9999, config.ObsPort);
        // All other fields should have their defaults
        Assert.Equal("", config.ObsPassword);
        Assert.False(config.AutoStartBuffer);
        Assert.Equal("f10", config.ToggleHotkey);
    }

    [Fact]
    public void RoundTrip_SerializeDeserialize()
    {
        var original = new AppConfig
        {
            ObsPort = 1234,
            ObsPassword = "test",
            ToggleHotkey = "ctrl+f5",
            SaveHotkey = "num multiply",
            ShowNotifications = false,
            NotificationDuration = 5.5,
            OrganizeByGame = false,
            OverlayX = 100,
            OverlayY = 200
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.ObsPort, restored.ObsPort);
        Assert.Equal(original.ObsPassword, restored.ObsPassword);
        Assert.Equal(original.ToggleHotkey, restored.ToggleHotkey);
        Assert.Equal(original.SaveHotkey, restored.SaveHotkey);
        Assert.Equal(original.ShowNotifications, restored.ShowNotifications);
        Assert.Equal(original.NotificationDuration, restored.NotificationDuration);
        Assert.Equal(original.OrganizeByGame, restored.OrganizeByGame);
        Assert.Equal(original.OverlayX, restored.OverlayX);
        Assert.Equal(original.OverlayY, restored.OverlayY);
    }

    [Fact]
    public void OverlayPosition_DefaultsToNull()
    {
        var config = new AppConfig();
        Assert.Null(config.OverlayX);
        Assert.Null(config.OverlayY);
    }

    [Fact]
    public void WatchFolder_DefaultIsNotNull()
    {
        var config = new AppConfig();
        Assert.NotNull(config.WatchFolder);
        Assert.Contains("Videos", config.WatchFolder);
    }
}
