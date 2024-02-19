using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common
{
    internal class ExceptionConverter : JsonConverter<Exception?>
    {
        private const string s_notAvailable = "<n/a>";
        private const string s_typePropertName = "$type";

        public override Exception? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Exception? result = null;
            if(reader.TokenType is JsonTokenType.StartObject)
            {
                while(reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString()!;
                    if(propertyName.Equals(s_typePropertName, StringComparison.OrdinalIgnoreCase))
                    {
                        Type type = Assembly.GetExecutingAssembly().GetType(reader.GetString()!)!;
                        result = (Exception)Activator.CreateInstance(type)!;
                    }
                    if (!reader.Read())
                    {
                        throw new JsonException();
                    }
                    if(reader.TokenType is not JsonTokenType.String || reader.GetString() != s_notAvailable)
                    {
                        PropertyInfo pi = typeToConvert.GetProperty(propertyName)!;
                        pi.SetValue(result, JsonSerializer.Deserialize(ref reader, pi.PropertyType, options));
                    }
                }
                if(reader.TokenType is not JsonTokenType.EndObject)
                {
                    throw new JsonException();
                }
            }
            else if(reader.TokenType is JsonTokenType.Null) { }
            else
            {
                throw new JsonException();
            }
            return result;
        }

        public override void Write(Utf8JsonWriter writer, Exception? value, JsonSerializerOptions options)
        {
            if(value is null)
            {
                writer.WriteNullValue();
            }
            else 
            {
                writer.WriteStartObject();
                writer.WriteString(s_typePropertName, value.GetType().FullName);
                foreach(PropertyInfo pi in value.GetType().GetProperties())
                {
                    writer.WritePropertyName(pi.Name);
                    try
                    {
                        JsonSerializer.Serialize(writer, pi.GetValue(value), options);
                    }
                    catch(Exception)
                    {
                        writer.WriteStringValue(s_notAvailable);
                    }
                }
                writer.WriteEndObject();
            }
        }
    }
}