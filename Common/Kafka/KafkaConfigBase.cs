namespace Net.Leksi.MicroService.Common;

public abstract class KafkaConfigBase
{
    public Dictionary<string, string> Properties { get; private init; } = [];
}
