namespace Net.Leksi.MicroService.Common;

public class KafkaMessageBase
{
    public string? Key { get; internal set; }
    public DateTime? Timestamp { get; internal set; }
    public string? Sender { get; internal set; }
    public WorkerId? WorkerId { get; internal set; }
}
