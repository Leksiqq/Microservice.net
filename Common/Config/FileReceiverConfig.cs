namespace Net.Leksi.MicroService.Common;
public class FileReceiverConfig: TemplateWorkerConfig
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string Login { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Folder { get; set; } = null!;
    public bool DeleteAfterDownload { get; set; } = false;
}

