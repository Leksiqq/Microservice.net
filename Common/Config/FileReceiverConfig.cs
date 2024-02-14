namespace Net.Leksi.MicroService.Common;
public class FileReceiverConfig: TemplateWorkerConfig
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Login { get; set; }
    public string? Password { get; set; }
    public string? Folder { get; set; }
    public bool? DeleteAfterDownload { get; set; }
}

