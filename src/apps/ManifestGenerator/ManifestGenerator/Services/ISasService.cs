namespace ManifestGenerator.Services;

public interface ISasService
{
    public Task<string> GenerateSasAsync(string blobContainerName, string sasDirectory, int sasExpirationMonths);
}
