using MimeKit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

internal class MimeMessageJsonConverter : JsonConverter<MimeMessage>
{
    public override MimeMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, MimeMessage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteHeaders(writer, value.Headers, options);

        MimeIterator iter = new(value);

        bool partsStarted = false;

        while (iter.MoveNext())
        {
            if (!partsStarted)
            {
                partsStarted = true;
                writer.WritePropertyName("parts");
                writer.WriteStartArray();
            }
            if (iter.Current is MimePart part)
            {
                writer.WriteStartObject();

                WriteHeaders(writer, part.Headers, options);

                MemoryStream ms = new();
                part.Content.WriteTo(ms);
                ms.Flush();
                ms.Position = 0;
                writer.WriteString("content", ms.ToArray());

                writer.WriteEndObject();
            }
        }
        if(partsStarted)
        {
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WriteHeaders(Utf8JsonWriter writer, HeaderList headers, JsonSerializerOptions options)
    {
        if (headers.Count > 0)
        {
            writer.WritePropertyName("headers");
            writer.WriteStartArray();
            foreach (Header h in headers)
            {
                writer.WriteStartObject();
                writer.WriteString("name", h.Field);
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, h.Value, options);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }
}