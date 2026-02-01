using System.Text.Json;
using OBSReplay.Host.Models;
using Xunit;

namespace OBSReplay.Host.Tests.Models;

public class IpcMessageTests
{
    [Fact]
    public void Create_WithNoPayload_HasEmptyJsonObject()
    {
        var msg = IpcMessage.Create("show_overlay");

        Assert.Equal("show_overlay", msg.Type);
        Assert.Equal("{}", msg.Payload);
    }

    [Fact]
    public void Create_WithPayload_SerializesCorrectly()
    {
        var msg = IpcMessage.Create("switch_scene", new { name = "Gaming" });

        Assert.Equal("switch_scene", msg.Type);
        Assert.Contains("\"name\"", msg.Payload);
        Assert.Contains("Gaming", msg.Payload);
    }

    [Fact]
    public void Create_WithComplexPayload_SerializesCorrectly()
    {
        var msg = IpcMessage.Create("toggle_source", new
        {
            scene = "Main",
            itemId = 42,
            visible = true
        });

        Assert.Equal("toggle_source", msg.Type);

        var payload = JsonDocument.Parse(msg.Payload);
        Assert.Equal("Main", payload.RootElement.GetProperty("scene").GetString());
        Assert.Equal(42, payload.RootElement.GetProperty("itemId").GetInt32());
        Assert.True(payload.RootElement.GetProperty("visible").GetBoolean());
    }

    [Fact]
    public void IpcMessage_SerializationRoundTrip()
    {
        var original = IpcMessage.Create("state_update", new { connected = true });
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<IpcMessage>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("state_update", deserialized!.Type);
        Assert.Contains("connected", deserialized.Payload);
    }

    [Fact]
    public void Create_WithObsState_SerializesAllFields()
    {
        var state = new ObsState
        {
            Connected = true,
            Scenes = ["Scene1", "Scene2"],
            CurrentScene = "Scene1",
            Sources =
            [
                new SceneItem { Id = 1, Name = "Camera", IsVisible = true }
            ],
            Audio =
            [
                new AudioSource { Name = "Mic", VolumeMul = 0.8, IsMuted = false }
            ],
            IsStreaming = false,
            IsRecording = true,
            IsBufferActive = true,
            HasActiveCapture = true,
        };

        var msg = IpcMessage.Create("state_update", state);
        var payload = JsonDocument.Parse(msg.Payload);

        Assert.True(payload.RootElement.GetProperty("connected").GetBoolean());
        Assert.Equal(2, payload.RootElement.GetProperty("scenes").GetArrayLength());
        Assert.Equal("Scene1", payload.RootElement.GetProperty("currentScene").GetString());
        Assert.True(payload.RootElement.GetProperty("isBufferActive").GetBoolean());
    }
}
