using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

public class KafkaConfigJsonConverter : JsonConverter<KafkaConfig>
{
    public override KafkaConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        KafkaConfig result;
        if(typeToConvert == typeof(KafkaLoggerConfig))
        {
            result = new KafkaLoggerConfig();
        }
        else
        {
            result = new KafkaConfig();
        }
        if (reader.TokenType is JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                {
                    throw new JsonException("Property name missed!");
                }
                string propertyName = reader.GetString() ?? throw new JsonException("Property name missed!");
                if (!reader.Read())
                {
                    throw new JsonException("Property value missed!");
                }
                if(propertyName.ToLower() == "topics")
                {
                    if (reader.TokenType is JsonTokenType.String)
                    {
                        result.Topics.AddRange(reader.GetString()?.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []);
                    }
                    else if (reader.TokenType is JsonTokenType.StartArray)
                    {
                        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
                        {
                            if(reader.GetString() is string topic)
                            {
                                result.Topics.Add(topic);
                            }
                        }
                    }
                    else
                    {
                        throw new JsonException("'Topics can only be string or array!");
                    }
                }
                else
                {
                    if (reader.GetString() is string value)
                    {
                        result.Properties.Add(propertyName, value);
                    }
                }
            }
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, KafkaConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach(var it in value.Properties)
        {
            writer.WriteString(it.Key, it.Value);
        }
        writer.WriteEndObject();
        writer.WritePropertyName("topics");
        writer.WriteStartArray();
        foreach (var it in value.Topics)
        {
            writer.WriteStringValue(it);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
