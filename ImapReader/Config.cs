using Net.Leksi.MicroService.Common;

namespace Net.Leksi.MicroService.ImapReader;
public class Config: TemplateWorkerConfig
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string Login { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Folder { get; set; } = null!;
}
