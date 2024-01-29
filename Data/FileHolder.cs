using System.Text;

namespace Net.Leksi.MicroService;

public class FileHolder:HeadersHolder
{
    public byte[]? Content { get; set; }
}