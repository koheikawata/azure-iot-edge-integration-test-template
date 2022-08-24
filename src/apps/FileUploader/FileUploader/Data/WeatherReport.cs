using System.Text.Json;

namespace FileUploader.Data;

public class WeatherReport : UploadData
{
    public override string FolderName => "reports";
    public override bool IsValidFile => Path.GetExtension(this.FileName) == ".zip" && File.Exists(this.FileFullPath + ".json");


    public override async Task UploadMetadataAsync()
    {
        if (this.BlobClient is null)
        {
            throw new ArgumentNullException(nameof(this.BlobClient));
        }

        string jsonString = File.ReadAllText(this.FileFullPath + ".json");
        WeatherReportMetaData weatherReportMetaData = JsonSerializer.Deserialize<WeatherReportMetaData>(jsonString)!;

        Dictionary<string, string> metadata = new Dictionary<string, string>();
        metadata["report_time"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
        metadata["country"] = weatherReportMetaData.Country!;
        metadata["city"] = weatherReportMetaData.City!;
        metadata["temperature"] = weatherReportMetaData.TemperatureC.ToString();
        metadata["summary"] = weatherReportMetaData.Summary!;

        await this.BlobClient.SetMetadataAsync(metadata).ConfigureAwait(false);
    }

    public override void DeleteFile()
    {
        base.DeleteFile();
        File.Delete(this.FileFullPath + ".json");
    }
}
