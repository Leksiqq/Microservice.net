using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

public class KafkaMessageConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(KafkaMessageBase).IsAssignableFrom(typeToConvert);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new KafkaMessageConverter();
    }
}
