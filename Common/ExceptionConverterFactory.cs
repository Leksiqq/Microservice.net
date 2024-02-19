using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

public class ExceptionConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(Exception).IsAssignableFrom(typeToConvert);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new ExceptionConverter();
    }
}
