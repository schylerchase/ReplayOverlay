using OBSReplay.Host.Services;
using Xunit;

namespace OBSReplay.Host.Tests.Services;

public class ObsHotkeySyncServiceTests
{
    [Theory]
    [InlineData("OBS_KEY_F1", "f1")]
    [InlineData("OBS_KEY_F9", "f9")]
    [InlineData("OBS_KEY_F12", "f12")]
    [InlineData("OBS_KEY_F16", "f16")]
    public void MapObsKeyName_FunctionKeys(string obsKey, string expected)
    {
        Assert.Equal(expected, ObsHotkeySyncService.MapObsKeyName(obsKey));
    }

    [Theory]
    [InlineData("OBS_KEY_NUMPLUS", "num add")]
    [InlineData("OBS_KEY_NUMPADADD", "num add")]
    [InlineData("OBS_KEY_NUMPAD_ADD", "num add")]
    [InlineData("OBS_KEY_NUMMINUS", "num subtract")]
    [InlineData("OBS_KEY_NUMPADSUBTRACT", "num subtract")]
    [InlineData("OBS_KEY_NUMPAD_SUBTRACT", "num subtract")]
    [InlineData("OBS_KEY_NUMASTERISK", "num multiply")]
    [InlineData("OBS_KEY_NUMPADMULTIPLY", "num multiply")]
    [InlineData("OBS_KEY_NUMSLASH", "num divide")]
    [InlineData("OBS_KEY_NUMPADDIVIDE", "num divide")]
    [InlineData("OBS_KEY_NUMPERIOD", "num decimal")]
    [InlineData("OBS_KEY_NUMPADENTER", "num enter")]
    [InlineData("OBS_KEY_NUMPAD0", "num 0")]
    [InlineData("OBS_KEY_NUMPAD9", "num 9")]
    public void MapObsKeyName_NumpadKeys(string obsKey, string expected)
    {
        Assert.Equal(expected, ObsHotkeySyncService.MapObsKeyName(obsKey));
    }

    [Theory]
    [InlineData("OBS_KEY_SPACE", "space")]
    [InlineData("OBS_KEY_RETURN", "enter")]
    [InlineData("OBS_KEY_ENTER", "enter")]
    [InlineData("OBS_KEY_ESCAPE", "escape")]
    [InlineData("OBS_KEY_TAB", "tab")]
    [InlineData("OBS_KEY_BACKSPACE", "backspace")]
    [InlineData("OBS_KEY_DELETE", "delete")]
    [InlineData("OBS_KEY_INSERT", "insert")]
    [InlineData("OBS_KEY_HOME", "home")]
    [InlineData("OBS_KEY_END", "end")]
    [InlineData("OBS_KEY_PAGEUP", "pageup")]
    [InlineData("OBS_KEY_PAGEDOWN", "pagedown")]
    [InlineData("OBS_KEY_UP", "up")]
    [InlineData("OBS_KEY_DOWN", "down")]
    [InlineData("OBS_KEY_LEFT", "left")]
    [InlineData("OBS_KEY_RIGHT", "right")]
    [InlineData("OBS_KEY_PRINT", "printscreen")]
    [InlineData("OBS_KEY_SCROLLLOCK", "scrolllock")]
    [InlineData("OBS_KEY_PAUSE", "pause")]
    public void MapObsKeyName_CommonKeys(string obsKey, string expected)
    {
        Assert.Equal(expected, ObsHotkeySyncService.MapObsKeyName(obsKey));
    }

    [Theory]
    [InlineData("OBS_KEY_A", "a")]
    [InlineData("OBS_KEY_Z", "z")]
    [InlineData("OBS_KEY_0", "0")]
    [InlineData("OBS_KEY_9", "9")]
    public void MapObsKeyName_SingleLetterDigit(string obsKey, string expected)
    {
        Assert.Equal(expected, ObsHotkeySyncService.MapObsKeyName(obsKey));
    }

    [Fact]
    public void MapObsKeyName_UnknownKeyReturnsNull()
    {
        Assert.Null(ObsHotkeySyncService.MapObsKeyName("OBS_KEY_UNKNOWN_THING"));
    }

    [Fact]
    public void MapObsKeyName_WithoutPrefix_StillWorks()
    {
        // The method strips OBS_KEY_ prefix, but should still work if it's already stripped
        Assert.Equal("f1", ObsHotkeySyncService.MapObsKeyName("F1"));
    }

    [Fact]
    public void MapObsKeyName_EmptyString_ReturnsNull()
    {
        Assert.Null(ObsHotkeySyncService.MapObsKeyName(""));
    }

    [Fact]
    public void ExtractHotkeyFromJson_ObsFormat_NumPlus()
    {
        // Actual format from OBS 32: ReplayBuffer={"ReplayBuffer.Save":[{"key":"OBS_KEY_NUMPLUS"}]}
        var json = """{"ReplayBuffer.Save":[{"key":"OBS_KEY_NUMPLUS"}]}""";
        var result = ObsHotkeySyncService.ExtractHotkeyFromJson(json, "ReplayBuffer.Save");
        Assert.Equal("num add", result);
    }

    [Fact]
    public void ExtractHotkeyFromJson_ObsFormat_FunctionKey()
    {
        var json = """{"ReplayBuffer.Save":[{"key":"OBS_KEY_F9"}]}""";
        var result = ObsHotkeySyncService.ExtractHotkeyFromJson(json, "ReplayBuffer.Save");
        Assert.Equal("f9", result);
    }

    [Fact]
    public void ExtractHotkeyFromJson_EmptyArray_ReturnsNull()
    {
        var json = """{"ReplayBuffer.Save":[]}""";
        var result = ObsHotkeySyncService.ExtractHotkeyFromJson(json, "ReplayBuffer.Save");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractHotkeyFromJson_MissingProperty_ReturnsNull()
    {
        var json = """{"SomeOther":[{"key":"OBS_KEY_F9"}]}""";
        var result = ObsHotkeySyncService.ExtractHotkeyFromJson(json, "ReplayBuffer.Save");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractHotkeyFromJson_InvalidJson_ReturnsNull()
    {
        var result = ObsHotkeySyncService.ExtractHotkeyFromJson("not json", "ReplayBuffer.Save");
        Assert.Null(result);
    }

    [Fact]
    public void ReadObsReplayHotkey_ReturnsNullOrString()
    {
        // This reads from real OBS config; on CI or machines without OBS, returns null
        var result = ObsHotkeySyncService.ReadObsReplayHotkey();
        // Just verify it doesn't throw and returns the right type
        Assert.True(result == null || result.Length > 0);
    }

    [Fact]
    public void ReadObsRecordDirectory_ReturnsNullOrValidPath()
    {
        var result = ObsHotkeySyncService.ReadObsRecordDirectory();
        // Should return null or an existing directory
        Assert.True(result == null || System.IO.Directory.Exists(result));
    }
}
