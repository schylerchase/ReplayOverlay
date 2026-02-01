using System.Text.Json.Serialization;

namespace OBSReplay.Host.Models;

public class ObsStatsData
{
    [JsonPropertyName("cpuUsage")]
    public double CpuUsage { get; set; }

    [JsonPropertyName("memoryUsage")]
    public double MemoryUsage { get; set; }

    [JsonPropertyName("availableDiskSpace")]
    public double AvailableDiskSpace { get; set; }

    [JsonPropertyName("activeFps")]
    public double ActiveFps { get; set; }

    [JsonPropertyName("averageFrameRenderTime")]
    public double AverageFrameRenderTime { get; set; }

    [JsonPropertyName("renderSkippedFrames")]
    public int RenderSkippedFrames { get; set; }

    [JsonPropertyName("renderTotalFrames")]
    public int RenderTotalFrames { get; set; }

    [JsonPropertyName("outputSkippedFrames")]
    public int OutputSkippedFrames { get; set; }

    [JsonPropertyName("outputTotalFrames")]
    public int OutputTotalFrames { get; set; }
}
