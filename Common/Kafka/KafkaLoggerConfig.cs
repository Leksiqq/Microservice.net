using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

[JsonConverter(typeof(KafkaConfigJsonConverter))]
public class KafkaLoggerConfig: KafkaConfig
{
    public List<string> InformationTopics { get; private init; } = [];
    public List<string> WarningTopics { get; private init; } = [];
    public List<string> ErrorTopics { get; private init; } = [];
    public List<string> CriticalTopics { get; private init; } = [];
    public List<string> TraceTopics { get; private init; } = [];
    public List<string> DebugTopics { get; private init; } = [];

}
