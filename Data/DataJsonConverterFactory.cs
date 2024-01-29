using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService;

public class DataJsonConverterFactory : JsonConverterFactory
{
    private static readonly Dictionary<Type, Func<JsonConverter>> s_types = new()
    {
        {typeof(FileHolder), () => new FileHolderConverter()},
        {typeof(FilesHolder), () => new FilesHolderConverter()}
    };
    public override bool CanConvert(Type typeToConvert)
    {
        return s_types.ContainsKey(typeToConvert);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return s_types[typeToConvert]();
    }
}
