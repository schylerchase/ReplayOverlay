using System.Text.Json.Serialization;

namespace ReplayOverlay.Host.Models;

public class FilterInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }
}
