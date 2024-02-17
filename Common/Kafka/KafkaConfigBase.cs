namespace Net.Leksi.MicroService.Common;

public abstract class KafkaConfigBase
{
    public Dictionary<string, string> Properties { get; private init; } = [];
    public int Timeout { get; set; } = 10000;
}
