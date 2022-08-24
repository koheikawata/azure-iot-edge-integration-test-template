using Azure.Storage.Blobs;
using System.Reflection;

namespace FileUploader.Data;

public abstract class UploadData
{
    public abstract string FolderName { get; }
    public abstract bool IsValidFile { get; }
    public string? FileFullPath { get; set; }
    public string? FileName => Path.GetFileName(FileFullPath);
    public BlobClient? BlobClient { get; set; }

    public virtual async Task UploadBlobAsync()
    {
        await this.BlobClient!.UploadAsync(this.FileFullPath, true).ConfigureAwait(false);
    }
    public abstract Task UploadMetadataAsync();

    public virtual void DeleteFile()
    {
        File.Delete(this.FileFullPath!);
    }
}
