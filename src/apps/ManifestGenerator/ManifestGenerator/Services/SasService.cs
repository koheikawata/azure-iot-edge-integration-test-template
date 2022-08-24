using Azure.Storage.Files.DataLake;
using Azure.Storage.Sas;

namespace ManifestGenerator.Services;

public class SasService : ISasService
{
    private readonly DataLakeServiceClient dataLakeServiceClient;
    private DataLakeSasPermissions dataLakeSasPermissions = DataLakeSasPermissions.Read | DataLakeSasPermissions.Write | DataLakeSasPermissions.Create;

    public SasService(DataLakeServiceClient dataLakeServiceClient)
    {
        this.dataLakeServiceClient = dataLakeServiceClient;
    }

    public async Task<string> GenerateSasAsync(string blobContainerName, string sasDirectory, int sasExpirationMonths)
    {
        DataLakeFileSystemClient fileSystemClient = this.dataLakeServiceClient.GetFileSystemClient(blobContainerName);
        if (!await fileSystemClient.ExistsAsync().ConfigureAwait(false))
        {
            await this.dataLakeServiceClient.CreateFileSystemAsync(blobContainerName).ConfigureAwait(false);
        }

        DataLakeDirectoryClient directoryClient = await fileSystemClient.CreateDirectoryAsync(sasDirectory);
        DataLakeSasBuilder sasBuilder = new ()
        {
            FileSystemName = directoryClient.FileSystemName,
            Resource = "d",
            IsDirectory = true,
            Path = directoryClient.Path,
        };
        sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddMonths(sasExpirationMonths);
        sasBuilder.SetPermissions(dataLakeSasPermissions);
        string sasUri = directoryClient.GenerateSasUri(sasBuilder).ToString();
        string[] sasContents = sasUri.Split('?');
        string sasConnectionString = $"BlobEndpoint=https://{this.dataLakeServiceClient.AccountName}.blob.core.windows.net/{blobContainerName}/{sasDirectory};SharedAccessSignature={sasContents[1]}";

        return sasConnectionString;
    }
}
