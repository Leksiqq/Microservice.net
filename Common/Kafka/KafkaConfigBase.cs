using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

[JsonConverter(typeof(KafkaConfigJsonConverter))]
public abstract class KafkaConfigBase
{
    public Dictionary<string, string> Properties { get; private init; } = [];
}
