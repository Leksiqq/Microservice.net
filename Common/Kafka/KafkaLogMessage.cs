using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLogMessage
{
    public string? Sender { get; internal set; }
    public LogLevel LogLevel { get; internal set; }
    public EventId EventId { get; internal set; }
    public object? State { get; internal set; }
    public Exception? Exception { get; internal set; }
    public string? Message { get; internal set;}
}
