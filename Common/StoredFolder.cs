using Microsoft.Extensions.DependencyInjection;

namespace Net.Leksi.MicroService.Common;

public class StoredFolder(IServiceProvider services)
{
    public string? UserName { get; private init; } = services.GetService<WorkerId>()?.Name;
    public DateTime Timestamp { get; private init; } = DateTime.UtcNow;
    public List<KeyValuePair<string, string>> Headers { get; private init; } = [];
    public List<StoredFile> Files { get; private init; } = [];
}
