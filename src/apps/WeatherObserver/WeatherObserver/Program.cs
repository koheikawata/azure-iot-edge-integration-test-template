using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using WeatherObserver.Models;

namespace WeatherObserver;

public class Program
{
    static string routeC2W = "reportRequest";
    static string routeW2C = "reportResponse";

    public static async Task Main(string[] args)
    {
        Init().Wait();

        CancellationTokenSource cts = new ();
        AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
        Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
        await WhenCancelled(cts.Token).ConfigureAwait(false);
    }

    public static async Task Init()
    {
        MqttTransportSettings mqttSetting = new (TransportType.Mqtt_Tcp_Only);
        ITransportSettings[] settings = { mqttSetting };
        ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings).ConfigureAwait(false);
        await ioTHubModuleClient.OpenAsync().ConfigureAwait(false);
        await ioTHubModuleClient.SetInputMessageHandlerAsync(routeC2W, ParseRequests, ioTHubModuleClient).ConfigureAwait(false);
    }

    public static async Task<MessageResponse> ParseRequests(Message message, object userContext)
    {
        ModuleClient? moduleClient = userContext as ModuleClient;
        if (moduleClient is null)
        {
            throw new InvalidOperationException("UserContext doesn't contain expected values");
        }

        string messageString = Encoding.UTF8.GetString(message.GetBytes());
        Weather? weatherRequested = JsonSerializer.Deserialize<Weather>(messageString);

        if (weatherRequested is null)
        {
            throw new InvalidOperationException("Request doesn't contain expected values");
        }

        Weather weatherTokyo = new () { Id = Guid.NewGuid().ToString(), Country = "Japan", City = "Tokyo", TemperatureC = 35, Summary = "Sunny", };
        Weather weatherParis = new () { Id = Guid.NewGuid().ToString(), Country = "France", City = "Paris", TemperatureC = 18, Summary = "Cloudy", };
        Weather weatherSeattle = new () { Id = Guid.NewGuid().ToString(), Country = "US", City = "Seattle", TemperatureC = 15, Summary = "Rainy", };
        Weather weatherAuckland = new () { Id = Guid.NewGuid().ToString(), Country = "NZ", City = "Auckland", TemperatureC = 5, Summary = "Sunny", };

        Weather weatherReported = new ();

        switch (weatherRequested.City)
        {
            case "Tokyo":
                weatherReported = weatherTokyo;
                break;
            case "Paris":
                weatherReported = weatherParis;
                break;
            case "Seattle":
                weatherReported = weatherSeattle;
                break;
            case "Auckland":
                weatherReported = weatherAuckland;
                break;
            default:
                break;
        }

        string jsonString = JsonSerializer.Serialize(weatherReported, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"Sent weather report: {jsonString}");
        return await SendMessageAsync(moduleClient, weatherReported).ConfigureAwait(false);
    }

    public static async Task<MessageResponse> SendMessageAsync(ModuleClient moduleClient, Weather weatherReported)
    {
        string messageString = JsonSerializer.Serialize(weatherReported);

        if (messageString is not null)
        {
            using (Message message = new (Encoding.ASCII.GetBytes(messageString)))
            {
                await moduleClient.SendEventAsync(routeW2C, message).ConfigureAwait(false);
            }
        }

        return MessageResponse.Completed;
    }

    public static Task WhenCancelled(CancellationToken cancellationToken)
    {
        TaskCompletionSource tcs = new ();
        cancellationToken.Register(() => tcs.SetResult());
        return tcs.Task;
    }
}
