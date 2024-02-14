namespace Net.Leksi.MicroService.Common;

public abstract class KafkaConfigBase
{
    public Dictionary<string, string> Properties { get; private init; } = [];
    public string? Sender { get; set; }
    public TimeSpan TransactionsTimeout { get; set; } = new(0, 0, 10);
}
