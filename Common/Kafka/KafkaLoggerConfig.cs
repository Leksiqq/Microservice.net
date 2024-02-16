namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerConfig: KafkaProducerConfig
{
    public List<string> InformationTopics { get; private init; } = [];
    public List<string> WarningTopics { get; private init; } = [];
    public List<string> ErrorTopics { get; private init; } = [];
    public List<string> CriticalTopics { get; private init; } = [];
    public List<string> TraceTopics { get; private init; } = [];
    public List<string> DebugTopics { get; private init; } = [];

}
