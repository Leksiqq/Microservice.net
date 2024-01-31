using MimeKit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService;

public class MimeMessageJsonConverter : JsonConverter<MimeMessage>
{
    public override MimeMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, MimeMessage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if(value.Headers.Count > 0)
        {
            writer.WritePropertyName("headers");
            writer.WriteStartObject();
            foreach (Header h in value.Headers)
            {
                writer.WritePropertyName(h.Field);
                JsonSerializer.Serialize(writer, h.Value, options);
            }
            writer.WriteEndObject();
        }

        MimeIterator iter = new(value);

        bool partsStarted = false;

        while (iter.MoveNext())
        {
            if (!partsStarted)
            {
                partsStarted = true;
                writer.WritePropertyName("parts");
                writer.WriteStartObject();
            }
            if (iter.Current is MimePart part)
            {
                if(part.Headers.Count > 0)
                {
                    writer.WritePropertyName("headers");
                    writer.WriteStartObject();
                    foreach (Header h in part.Headers)
                    {
                        writer.WritePropertyName(h.Field);
                        JsonSerializer.Serialize(writer, h.Value, options);
                    }
                    writer.WriteEndObject();
                }
                MemoryStream ms = new();
                part.Content.WriteTo(ms);
                ms.Flush();
                ms.Position = 0;
                writer.WriteBase64String("content", ms.ToArray());
            }
        }
        if(partsStarted)
        {
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}