using System.Text.Json.Serialization;

namespace OBSReplay.Host.Models;

public class AudioAdvancedInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Sync offset in milliseconds (OBS stores nanoseconds internally).</summary>
    [JsonPropertyName("syncOffsetMs")]
    public int SyncOffsetMs { get; set; }

    /// <summary>Balance 0.0 (full left) to 1.0 (full right), 0.5 = center.</summary>
    [JsonPropertyName("balance")]
    public double Balance { get; set; } = 0.5;

    /// <summary>Monitor type: 0=None, 1=MonitorOnly, 2=MonitorAndOutput.</summary>
    [JsonPropertyName("monitorType")]
    public int MonitorType { get; set; }

    /// <summary>Audio track routing, tracks 1-6 (index 0-5).</summary>
    [JsonPropertyName("tracks")]
    public bool[] Tracks { get; set; } = new bool[6];
}
