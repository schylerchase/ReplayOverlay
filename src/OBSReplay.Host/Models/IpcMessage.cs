using System.Text.Json.Serialization;

namespace OBSReplay.Host.Models;

public class IpcMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = "{}";

    public static IpcMessage Create(string type, object? payload = null)
    {
        return new IpcMessage
        {
            Type = type,
            Payload = payload != null
                ? System.Text.Json.JsonSerializer.Serialize(payload)
                : "{}"
        };
    }
}
