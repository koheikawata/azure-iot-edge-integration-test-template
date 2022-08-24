using System.Text.Json.Serialization;

namespace WeatherObserver.Models;

public class Weather
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("country")]
    public string? Country { get; set; }
    [JsonPropertyName("city")]
    public string? City { get; set; }
    [JsonPropertyName("temperatureC")]
    public int? TemperatureC { get; set; }
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}
