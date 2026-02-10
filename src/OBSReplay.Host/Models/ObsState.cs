using System.Text.Json.Serialization;

namespace OBSReplay.Host.Models;

public class ObsState
{
    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("scenes")]
    public List<string> Scenes { get; set; } = [];

    [JsonPropertyName("currentScene")]
    public string? CurrentScene { get; set; }

    [JsonPropertyName("sources")]
    public List<SceneItem> Sources { get; set; } = [];

    [JsonPropertyName("audio")]
    public List<AudioSource> Audio { get; set; } = [];

    [JsonPropertyName("isStreaming")]
    public bool IsStreaming { get; set; }

    [JsonPropertyName("isRecording")]
    public bool IsRecording { get; set; }

    [JsonPropertyName("isRecordingPaused")]
    public bool IsRecordingPaused { get; set; }

    [JsonPropertyName("isBufferActive")]
    public bool IsBufferActive { get; set; }

    [JsonPropertyName("isVirtualCamActive")]
    public bool IsVirtualCamActive { get; set; }

    [JsonPropertyName("hasActiveCapture")]
    public bool? HasActiveCapture { get; set; }

    // Transition & studio mode
    [JsonPropertyName("currentTransition")]
    public string? CurrentTransition { get; set; }

    [JsonPropertyName("transitionDuration")]
    public int TransitionDuration { get; set; }

    [JsonPropertyName("transitions")]
    public List<string> Transitions { get; set; } = [];

    [JsonPropertyName("studioModeEnabled")]
    public bool StudioModeEnabled { get; set; }

    [JsonPropertyName("previewScene")]
    public string? PreviewScene { get; set; }

    // Profiles & collections
    [JsonPropertyName("currentProfile")]
    public string? CurrentProfile { get; set; }

    [JsonPropertyName("currentSceneCollection")]
    public string? CurrentSceneCollection { get; set; }

    [JsonPropertyName("profiles")]
    public List<string> Profiles { get; set; } = [];

    [JsonPropertyName("sceneCollections")]
    public List<string> SceneCollections { get; set; } = [];
}
