using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoEFactura.Services.Api;

/// <summary>
/// Shared <c>System.Text.Json</c> settings for deserializing ANAF responses. Case-insensitive
/// property names and a lenient string converter tolerate a numeric <c>id</c> value (ANAF sometimes
/// returns numbers where the previous client expected a JSON string) without breaking deserialization.
/// </summary>
internal static class AnafJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new AnafLenientStringConverter() }
    };
}

/// <summary>
/// Reads a JSON string, number (as raw text) or boolean into a <see cref="string"/>. Writes normally.
/// </summary>
internal sealed class AnafLenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.HasValueSequence
                ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
                : Encoding.UTF8.GetString(reader.ValueSpan),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            JsonTokenType.Null => null,
            _ => throw new JsonException(
                $"Cannot convert token type {reader.TokenType} to string.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
