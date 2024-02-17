using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public class KafkaConsumeEventArgs: EventArgs
{
    public ConsumeResult<string, string> ConsumeResult { get; internal set; } = null!;
    public bool Commit { get; set; } = false;
}
