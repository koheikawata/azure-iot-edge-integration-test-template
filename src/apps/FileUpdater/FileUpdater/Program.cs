using Azure;
using Azure.Storage.Blobs;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System.IO.Compression;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using FileUpdater.Models;

namespace FileUpdater;

public class Program
{
    static string workdir = Environment.GetEnvironmentVariable("WORKDIR") ?? string.Empty;
    static string routeC2U = "updateRequest";
    static string routeU2C = "updateResponse";

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
        Console.WriteLine("FileUpdater initialized.");

        await ioTHubModuleClient.SetInputMessageHandlerAsync(routeC2U, ReceiveMessage, ioTHubModuleClient).ConfigureAwait(false);
    }

    public static async Task<MessageResponse> ReceiveMessage(Message message, object userContext)
    {
        ModuleClient? moduleClient = userContext as ModuleClient;
        if (moduleClient is null)
        {
            throw new InvalidOperationException("UserContext doesn't contain expected values");
        }

        string messageString = Encoding.UTF8.GetString(message.GetBytes());
        WeatherFileInfo? weatherFileInfo = JsonSerializer.Deserialize<WeatherFileInfo>(messageString);
        Console.WriteLine($"Received {weatherFileInfo} update requests.");

        string status = string.Empty;
        string errorMessage = string.Empty;

        try
        {
            string zipFilePath = Path.Combine(workdir, "archive", $"{weatherFileInfo!.FileName}.zip");
            string fileDirectoryPath = Path.Combine(workdir, "archive");

            string[] sasContents = weatherFileInfo.BlobSasUrl.Split('?');
            AzureSasCredential azureSasCredential = new (sasContents[1]);
            Uri blobUri = new (sasContents[0]);
            BlobClient blobClient = new (blobUri, azureSasCredential, null);
            await blobClient.DownloadToAsync(zipFilePath).ConfigureAwait(false);

            if (!Directory.Exists(fileDirectoryPath))
            {
                Directory.CreateDirectory(fileDirectoryPath);
            }

            ZipFile.ExtractToDirectory(zipFilePath, fileDirectoryPath);
            string[] files = Directory.GetFiles(fileDirectoryPath);
            File.Delete(zipFilePath);
            foreach (string file in files)
            {
                Console.WriteLine($"{file}");
            }
            status = "succeeded";
        }
        catch (Exception ex)
        {
            Console.Write(ex.Message);
            status = "failed";
            errorMessage = ex.Message;
        }

        Console.WriteLine($"Sent to IothubConector status: {status}");
        return await SendStatusMessageAsync(moduleClient, status, errorMessage).ConfigureAwait(false);
    }

    public static async Task<MessageResponse> SendStatusMessageAsync(ModuleClient moduleClient, string status, string errorMessage)
    {
        StatusInfo statusInfo = new ()
        {
            Status = status,
            Message = errorMessage,
        };
        string messageString = JsonSerializer.Serialize(statusInfo);

        if (messageString is not null)
        {
            using (Message message = new (Encoding.ASCII.GetBytes(messageString)))
            {
                await moduleClient.SendEventAsync(routeU2C, message);
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
