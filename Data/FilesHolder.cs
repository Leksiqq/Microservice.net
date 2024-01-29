namespace Net.Leksi.MicroService;

public class FilesHolder: HeadersHolder
{
    public List<FileHolder> Files { get; private init; } = [];
}
