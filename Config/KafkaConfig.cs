using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService;

[JsonConverter(typeof(KafkaConfigJsonConverter))]
public class KafkaConfig
{
    public Dictionary<string, string> Properties { get; private init; } = [];
    public List<string> Topics { get; private init; } = [];
}
