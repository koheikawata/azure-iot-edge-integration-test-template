using Azure.Storage.Blobs;
using FileUploader.Data;
using System.Runtime.Loader;

namespace FileUploader;

public class Program
{
    static string? accountName = Environment.GetEnvironmentVariable("LOCAL_STORAGE_ACCOUNT_NAME");
    static string? key = Environment.GetEnvironmentVariable("LOCAL_STORAGE_ACCOUNT_KEY");
    static string? blobEndpoint = Environment.GetEnvironmentVariable("LOCAL_STORAGE_BLOB_ENDPOINT");
    static string connectionString = $"DefaultEndpointsProtocol=http;AccountName={accountName};AccountKey={key};BlobEndpoint={blobEndpoint}/{accountName};";
    static string? localPath = Environment.GetEnvironmentVariable("WORKDIR");
    static string? containerName = "weather";

    static async Task Main(string[] args)
    {
        CancellationTokenSource cts = new ();
        AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
        Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

        BlobContainerClient containerClient = new (connectionString, containerName);
        List<UploadData> uploadDataList = new ()
        {
            new WeatherReport(),
        };
        while (!cts.Token.IsCancellationRequested)
        {
            await UploadFilesAsync(containerClient, uploadDataList).ConfigureAwait(false);
            await Task.Delay(5000);
        }

        await WhenCancelled(cts.Token).ConfigureAwait(false);
    }

    public static async Task UploadFilesAsync(BlobContainerClient containerClient, List<UploadData> uploadDataList)
    {
        try
        {
            await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nMessage\n{ex.Message}" +
                                    $"\nStackTrace\n{ex.StackTrace}");
        }

        foreach (UploadData uploadData in uploadDataList)
        {
            List<string> fileFullPaths = new ();

            try
            {
                fileFullPaths.AddRange(Directory.GetFiles(Path.Combine(localPath!, uploadData.FolderName), "*", SearchOption.AllDirectories));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nMessage\n{ex.Message}" +
                                        $"\nStackTrace\n{ex.StackTrace}");
                continue;
            }

            foreach (string fileFullPath in fileFullPaths)
            {
                Console.WriteLine($"Find: {fileFullPath}");
                try
                {
                    uploadData.FileFullPath = fileFullPath;
                    uploadData.BlobClient = containerClient.GetBlobClient(
                        $"{uploadData.FolderName}/{DateTime.Now.Year}/{DateTime.Now.Month}/{uploadData.FileName}");
                    if (uploadData.IsValidFile is false)
                    {
                        Console.WriteLine($"{fileFullPath} is not valid file");
                        continue;
                    }

                    await uploadData.UploadBlobAsync().ConfigureAwait(false);
                    Console.WriteLine($"Upload {fileFullPath}");
                    await uploadData.UploadMetadataAsync().ConfigureAwait(false);
                    uploadData.DeleteFile();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\nMessage\n{ex.Message}" +
                                            $"\nStackTrace\n{ex.StackTrace}");
                    continue;
                }
            }
        }
    }

    private static Task WhenCancelled(CancellationToken cancellationToken)
    {
        TaskCompletionSource tcs = new ();
        cancellationToken.Register(() => tcs.SetResult());
        return tcs.Task;
    }
}