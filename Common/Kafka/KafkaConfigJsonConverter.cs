using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

public class KafkaConfigJsonConverter : JsonConverter<KafkaConfigBase>
{
    private static readonly char[] separator = [' ', ','];

    public override KafkaConfigBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        KafkaConfigBase result;
        if(typeToConvert == typeof(KafkaLoggerConfig))
        {
            result = new KafkaLoggerConfig();
        }
        else if(typeToConvert == typeof(KafkaProducerConfig))
        {
            result = new KafkaProducerConfig();
        }
        else
        {
            result = new KafkaConsumerConfig();
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
                List<string>? targetList = null;
                if (
                    (
                        result is KafkaProducerConfig config
                        && propertyName.Equals(nameof(config.Topics), StringComparison.OrdinalIgnoreCase) 
                        && (targetList = config.Topics) == targetList
                    )
                    || (
                        result is KafkaLoggerConfig loggerConfig 
                        && (
                            (
                                propertyName.Equals(nameof(loggerConfig.InformationTopics), StringComparison.OrdinalIgnoreCase) 
                                && (targetList = loggerConfig.InformationTopics) == targetList
                            )
                            || (
                                propertyName.Equals(nameof(loggerConfig.WarningTopics), StringComparison.OrdinalIgnoreCase) 
                                && (targetList = loggerConfig.WarningTopics) == targetList
                            )
                            || (
                                propertyName.Equals(nameof(loggerConfig.CriticalTopics), StringComparison.OrdinalIgnoreCase) 
                                && (targetList = loggerConfig.CriticalTopics) == targetList
                            )
                            || (
                                propertyName.Equals(nameof(loggerConfig.ErrorTopics), StringComparison.OrdinalIgnoreCase) 
                                && (targetList = loggerConfig.ErrorTopics) == targetList
                            )
                            || (
                                propertyName.Equals(nameof(loggerConfig.DebugTopics), StringComparison.OrdinalIgnoreCase) 
                                && (targetList = loggerConfig.DebugTopics) == targetList
                            )
                            || (
                                propertyName.Equals(nameof(loggerConfig.TraceTopics), StringComparison.OrdinalIgnoreCase) 
                                && (targetList = loggerConfig.TraceTopics) == targetList
                            )
                        )
                    )
                )
                {
                    if (reader.TokenType is JsonTokenType.String)
                    {
                        targetList.AddRange(reader.GetString()?.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []);
                    }
                    else if (reader.TokenType is JsonTokenType.StartArray)
                    {
                        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
                        {
                            if(reader.GetString() is string topic)
                            {
                                targetList.Add(topic);
                            }
                        }
                    }
                    else
                    {
                        throw new JsonException("'*Topic' can only be string or array!");
                    }
                }
                else if (result is KafkaProducerConfig config1 && propertyName.Equals(nameof(config1.Sender), StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.GetString() is string value)
                    {
                        config1.Sender = value;
                    }
                }
                else if (result is KafkaConsumerConfig config2 && propertyName.Equals(nameof(config2.Topic), StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.GetString() is string value)
                    {
                        config2.Topic = value;
                    }
                }
                else if (result is KafkaConsumerConfig config3 && propertyName.Equals(nameof(config3.Group), StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.GetString() is string value)
                    {
                        config3.Group = value;
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

    public override void Write(Utf8JsonWriter writer, KafkaConfigBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(value.Properties).ToLower());
        writer.WriteStartObject();
        foreach (var it in value.Properties)
        {
            writer.WriteString(it.Key, it.Value);
        }
        writer.WriteEndObject();
        if (value is KafkaProducerConfig config)
        {
            writer.WriteString(nameof(config.Sender).ToLower(), config.Sender);
            WriteList(writer, nameof(config.Topics), config.Topics);
        }
        else if (value is KafkaConsumerConfig config1)
        {
            writer.WriteString(nameof(config1.Group).ToLower(), config1.Group);
            writer.WriteString(nameof(config1.Topic).ToLower(), config1.Topic);
        }
        else if (value is KafkaLoggerConfig loggerConfig)
        {
            WriteList(writer, nameof(loggerConfig.InformationTopics), loggerConfig.InformationTopics);
            WriteList(writer, nameof(loggerConfig.WarningTopics), loggerConfig.WarningTopics);
            WriteList(writer, nameof(loggerConfig.ErrorTopics), loggerConfig.ErrorTopics);
            WriteList(writer, nameof(loggerConfig.CriticalTopics), loggerConfig.CriticalTopics);
            WriteList(writer, nameof(loggerConfig.DebugTopics), loggerConfig.DebugTopics);
            WriteList(writer, nameof(loggerConfig.TraceTopics), loggerConfig.TraceTopics);
        }
        writer.WriteEndObject();
    }

    private static void WriteList(Utf8JsonWriter writer, string propertyName, List<string> list)
    {
        if(list.Count > 0)
        {
            writer.WritePropertyName(propertyName.ToLower());
            writer.WriteStartArray();
            foreach (var it in list)
            {
                writer.WriteStringValue(it);
            }
            writer.WriteEndArray();
        }
    }
}
