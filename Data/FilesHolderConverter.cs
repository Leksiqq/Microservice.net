using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService;

internal class FilesHolderConverter : JsonConverter<FilesHolder>
{
    public override FilesHolder? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, FilesHolder value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}