namespace Net.Leksi.MicroService.Common;

public class MinioConfig
{
    public string Endpoint { get; set; } = null!;
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public string Bucket { get; set; } = null!;
    public string? Folder { get; set; } = null;
}
