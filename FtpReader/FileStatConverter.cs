using Confluent.Kafka;
using MimeKit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.FtpReader;

internal class FileStatConverter : JsonConverter<FileStat>
{
    public override FileStat? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, FileStat value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();//{

        writer.WritePropertyName("parts");//parts=
        writer.WriteStartArray();//[

        writer.WriteStartObject();//{
        writer.WritePropertyName("headers");//headers=
        writer.WriteStartArray();//[

        writer.WriteStartObject();//{
        writer.WriteString("name", "Content-Length");
        writer.WriteNumber("value", value.Size);
        writer.WriteEndObject();//}

        writer.WriteStartObject();//{
        writer.WriteString("name", "Content-Transfer-Encoding");
        writer.WriteString("value", "base64");
        writer.WriteEndObject();//}

        writer.WriteStartObject();//{
        writer.WriteString("name", "Content-Type");
        writer.WriteString("value", $"application/octet-stream; name={value.Name}");
        writer.WriteString("value", "base64");
        writer.WriteEndObject();//}

        writer.WriteEndArray();//]

        writer.WriteString("content", value.Content);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}