using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.FtpReader;

[JsonConverter(typeof(FileStatConverter))]
public class FileStat
{
    public DateTime Created { get; set; }
    public DateTime ChangedSize { get; set; }
    public long Size { get; set; }
    public string FullName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public byte[] Content { get; set; } = null!;
}
