using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService;

public class FileHolderConverter : JsonConverter<FileHolder>
{
    public override FileHolder? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, FileHolder value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}