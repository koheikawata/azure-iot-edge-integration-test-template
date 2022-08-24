using System.Text.Json.Serialization;

namespace FileUpdater.Models;

public class WeatherFileInfo
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
    [JsonPropertyName("blobSasUrl")]
    public string BlobSasUrl { get; set; } = string.Empty;
}
