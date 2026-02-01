using System.Text.Json.Serialization;

namespace OBSReplay.Host.Models;

public class SceneItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; set; } = "";
}
