using System.Text.Json.Serialization;

namespace FileUpdater.Models;

public class StatusInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
