using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.FtpReader;

public class ListingSortConverter : JsonConverter<ListingSort?>
{
    public override ListingSort? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ListingSort? result;
        if(reader.TokenType is JsonTokenType.Null)
        {
            result = null;
        }
        else if(reader.TokenType is JsonTokenType.String)
        {
            ListingSort tmp;
            string value = reader.GetString()!;
            if (Enum.TryParse(value, out tmp))
            {
                result = (ListingSort?)tmp;
            }
            else
            {
                throw new JsonException($"Invalid value {nameof(ListingSort)}.{value}!");
            }
        }
        else
        {
            throw new JsonException($"Invalid value {nameof(ListingSort)}.{reader.GetString()}!");
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, ListingSort? value, JsonSerializerOptions options)
    {
        if(value is ListingSort ls)
        {
            writer.WriteStringValue(value.ToString());
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}