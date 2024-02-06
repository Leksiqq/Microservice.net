namespace Net.Leksi.MicroService.Common;

public interface ICloudClient: IDisposable
{
    string Bucket { get; }
    string Folder { get; }
    Task UploadFile(Stream stream, string path, string mimeType, long size, CancellationToken stoppingToken);
    Task<bool> FileExists(string path, CancellationToken stoppingToken);
}
