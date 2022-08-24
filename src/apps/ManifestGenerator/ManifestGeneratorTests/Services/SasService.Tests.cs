using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Azure.Storage.Sas;
using ManifestGenerator.Services;
using Moq;
using Xunit;

namespace ManifestGeneratorTests.Services;

public class SasServiceTests
{
    [Theory]
    [MemberData(nameof(DirectoryExistsAndExpectedTimes))]
    public async Task GenerateSasAsync_DirectoryExists_ConnectionString(bool directoryExist, Times expectedTimes)
    {
        // Arrange
        string blobContainerName = "weather";
        string orgName = "microsoft";
        int sasExpirationMonth = 6;
        string storageAccountName = "test1";
        string sas = "xxxxxx";
        string expected = $"BlobEndpoint=https://{storageAccountName}.blob.core.windows.net/{blobContainerName}/{orgName};SharedAccessSignature={sas}";
        Uri dummyUri = new ($"https://{storageAccountName}.blob.core.windows.net/weather/a/b.zip?{sas}");

        Mock<DataLakeDirectoryClient> mockedDataLakeDirectoryClient = new ();
        Mock<DataLakeFileSystemClient> mockedDataLakeFileSystemClient = new ();
        Mock<Response> mockedResponse = new ();
        Mock<DataLakeServiceClient> mockedDataLakeServiceClient = new ();

        mockedDataLakeDirectoryClient
            .Setup(x => x.GenerateSasUri(It.IsAny<DataLakeSasBuilder>()))
            .Returns(dummyUri);
        mockedDataLakeFileSystemClient
            .Setup(x => x.CreateDirectoryAsync(orgName, null, CancellationToken.None))
            .ReturnsAsync(Response.FromValue(mockedDataLakeDirectoryClient.Object, mockedResponse.Object));
        mockedDataLakeFileSystemClient
            .Setup(x => x.ExistsAsync(CancellationToken.None))
            .ReturnsAsync(Response.FromValue(directoryExist, mockedResponse.Object));
        mockedDataLakeServiceClient
            .Setup(x => x.GetFileSystemClient(blobContainerName))
            .Returns(mockedDataLakeFileSystemClient.Object);
        mockedDataLakeServiceClient
            .Setup(x => x.AccountName)
            .Returns(storageAccountName);

        SasService sasService = new (mockedDataLakeServiceClient.Object);

        // Act
        string result = await sasService.GenerateSasAsync(blobContainerName, orgName, sasExpirationMonth);

        // Assert
        Assert.Equal(expected, result);
        mockedDataLakeServiceClient.Verify(x => x.CreateFileSystemAsync(
                blobContainerName, PublicAccessType.None, null, CancellationToken.None), expectedTimes);
    }

    public static IEnumerable<object[]> DirectoryExistsAndExpectedTimes()
    {
        yield return new object[] { true, Times.Never() };
        yield return new object[] { false, Times.Once() };
    }
}
