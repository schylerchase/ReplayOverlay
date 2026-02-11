using System.Text.Json.Serialization;

namespace ReplayOverlay.Host.Models;

public class AudioSource
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("volumeMul")]
    public double VolumeMul { get; set; }

    [JsonPropertyName("isMuted")]
    public bool IsMuted { get; set; }
}
