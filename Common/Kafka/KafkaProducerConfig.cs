namespace Net.Leksi.MicroService.Common;

public class KafkaProducerConfig: KafkaConfigBase
{
    public string? Sender { get; set; }
    public List<string> Topics { get; private init; } = [];
}
