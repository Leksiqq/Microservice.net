namespace Net.Leksi.MicroService.Common;

public class StoredFile
{
    public List<KeyValuePair<string, string>> Headers { get; private init; } = [];
    public string Path { get; set; } = null!;
}
