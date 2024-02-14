namespace Net.Leksi.MicroService.Common;

public class StoredFolder
{
    public List<KeyValuePair<string, string>> Headers { get; private init; } = [];
    public List<StoredFile> Files { get; private init; } = [];
}
