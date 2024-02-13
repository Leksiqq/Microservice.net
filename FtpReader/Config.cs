using Net.Leksi.MicroService.Common;
using System.Text;

namespace Net.Leksi.MicroService.FtpReader;

public class Config: FileReceiverConfig
{
    public bool LogClient { get; set; } = false;
    public string? Encoding { get; set; }
    public string? Pattern { get; set; }
}