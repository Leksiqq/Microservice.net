namespace Net.Leksi.MicroService;

public class FilesHolder
{
    public Dictionary<string, object> Headers { get; private init; } = [];
    public List<FileHolder> Files { get; private init; } = [];
}
