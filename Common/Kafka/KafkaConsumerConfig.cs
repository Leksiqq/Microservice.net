namespace Net.Leksi.MicroService.Common;

public class KafkaConsumerConfig: KafkaConfigBase
{
    public string? Group { get; set; }
    public string? Topic { get; set; }
}
