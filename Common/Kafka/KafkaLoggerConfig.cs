namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerConfig: KafkaConfigBase
{
    public string Sender { get; set; } = string.Empty;
    public List<string> InformationTopics { get; private init; } = [];
    public List<string> WarningTopics { get; private init; } = [];
    public List<string> ErrorTopics { get; private init; } = [];
    public List<string> CriticalTopics { get; private init; } = [];
    public List<string> TraceTopics { get; private init; } = [];
    public List<string> DebugTopics { get; private init; } = [];

}
