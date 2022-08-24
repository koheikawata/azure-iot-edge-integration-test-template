using System.Text.Json.Serialization;

namespace FileUploader.Data;

public class WeatherReportMetaData
{
    [JsonPropertyName("country")]
    public string? Country { get; set; }
    [JsonPropertyName("city")]
    public string? City { get; set; }
    [JsonPropertyName("temperatureC")]
    public int TemperatureC { get; set; }
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    
}
