using System.Text;

namespace Net.Leksi.MicroService;

public class FileHolder
{
    public string? Path { get; set; }
    public string? MimeType { get; set; }
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public TransferEncoding TransferEncoding { get; set; } = TransferEncoding.Binary;
    public string? XToken { get; set; } = null;
    public byte[]? Content { get; set; }
}