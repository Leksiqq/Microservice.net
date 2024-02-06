namespace Net.Leksi.MicroService.Common;

public class KafkaConfig: KafkaConfigBase
{
    public List<string> Topics { get; private init; } = [];
}
