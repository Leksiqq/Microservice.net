using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

public class KafkaMessageConverter : JsonConverter<KafkaMessageBase>
{
    public override KafkaMessageBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        KafkaMessageBase? result = null;

        if(reader.TokenType is JsonTokenType.StartObject)
        {
            if (typeToConvert == typeof(KafkaLogMessage))
            {
                result = new KafkaLogMessage();
            }
            else 
            {
                throw new JsonException($"Converter for {typeToConvert} is not defined.");
            }
            while(reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
            {
                string prop = reader.GetString()!.ToLower();

                if (!reader.Read())
                {
                    throw new JsonException();
                }

                if(prop.Equals(nameof(result.Sender), StringComparison.OrdinalIgnoreCase))
                {
                    if(reader.TokenType is JsonTokenType.String)
                    {
                        result.Sender = reader.GetString();
                    }
                }
                else if (prop.Equals(nameof(result.Timestamp), StringComparison.OrdinalIgnoreCase))
                {
                    if(reader.TokenType is JsonTokenType.String && DateTime.TryParse(reader.GetString(), out DateTime timestamp))
                    {
                        result.Timestamp = timestamp;
                    }
                }
                else if (prop.Equals(nameof(result.Key), StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType is JsonTokenType.String)
                    {
                        result.Key = reader.GetString();
                    }
                }
                else if (prop.Equals(nameof(result.WorkerId), StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType is JsonTokenType.StartObject)
                    {
                        result.WorkerId = JsonSerializer.Deserialize<WorkerId>(ref reader, options);
                    }
                }
                else if(result is KafkaLogMessage logMessage)
                {
                    if(prop.Equals(nameof(logMessage.State), StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType is JsonTokenType.StartArray)
                        {
                            logMessage.State = JsonSerializer.Deserialize<object>(ref reader, options);
                        }
                    }
                    else if (prop.Equals(nameof(logMessage.LogLevel), StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType is JsonTokenType.Number)
                        {
                            logMessage.LogLevel = Enum.GetValues<LogLevel>()[reader.GetInt32()];
                        }
                    }
                    else if (prop.Equals(nameof(logMessage.EventId), StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType is JsonTokenType.StartObject)
                        {
                            logMessage.EventId = JsonSerializer.Deserialize<EventId>(ref reader, options);
                        }
                    }
                    else if (prop.Equals(nameof(logMessage.Message), StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType is JsonTokenType.String)
                        {
                            logMessage.Message = reader.GetString();
                        }
                    }
                    else if (prop.Equals(nameof(logMessage.Exception), StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType is JsonTokenType.StartObject)
                        {
                            logMessage.Exception = JsonSerializer.Deserialize<Exception>(ref reader, options);
                        }
                    }
                }
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, KafkaMessageBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.Key is { })
        {
            writer.WriteString(nameof(value.Key).ToLower(), value.Key);
        }
        else if (
            options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
            && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
        )
        {
            writer.WriteNull(nameof(value.Key).ToLower());
        }

        if (value.Sender is { })
        {
            writer.WriteString(nameof(value.Sender).ToLower(), value.Sender);
        }
        else if (
            options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
            && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
        )
        {
            writer.WriteNull(nameof(value.Sender).ToLower());
        }

        if (value.Timestamp is { })
        {
            writer.WriteString(nameof(value.Timestamp).ToLower(), $"{value.Timestamp:o}");
        }
        else if (
            options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
            && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
        )
        {
            writer.WriteNull(nameof(value.Timestamp).ToLower());
        }

        if (value.WorkerId is { })
        {
            writer.WritePropertyName(nameof(value.WorkerId).ToLower());
            JsonSerializer.Serialize(writer, value.WorkerId, options);
        }
        else if (
            options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
            && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
        )
        {
            writer.WriteNull(nameof(value.WorkerId).ToLower());
        }

        if (value is KafkaLogMessage logMessage)
        {
            writer.WriteNumber(nameof(logMessage.LogLevel).ToLower(), (int)logMessage.LogLevel);

            if (logMessage.EventId is { })
            {
                writer.WritePropertyName(nameof(logMessage.EventId).ToLower());
                JsonSerializer.Serialize(writer, logMessage.EventId, options);
            }
            else if (
                options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
                && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
            )
            {
                writer.WriteNull(nameof(logMessage.EventId).ToLower());
            }

            if (logMessage.State is { })
            {
                writer.WritePropertyName(nameof(logMessage.State).ToLower());
                JsonSerializer.Serialize(writer, logMessage.State, options);
            }
            else if (
                options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
                && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
            )
            {
                writer.WriteNull(nameof(logMessage.State).ToLower());
            }

            if (logMessage.Message is { })
            {
                writer.WriteString(nameof(logMessage.Message).ToLower(), logMessage.Message);
            }
            else if (
                options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
                && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
            )
            {
                writer.WriteNull(nameof(logMessage.Message).ToLower());
            }

            if (logMessage.Exception is { })
            {
                writer.WritePropertyName(nameof(logMessage.Exception).ToLower());
                JsonSerializer.Serialize(writer, logMessage.Exception, options);
            }
            else if (
                options.DefaultIgnoreCondition is not JsonIgnoreCondition.WhenWritingNull
                && options.DefaultIgnoreCondition is not JsonIgnoreCondition.Always
            )
            {
                writer.WriteNull(nameof(logMessage.Exception).ToLower());
            }
        }

        writer.WriteEndObject();
    }
}
