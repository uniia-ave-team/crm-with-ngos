using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crm.Api.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="DateTime"/> values as UTC (<see cref="DateTimeKind.Utc"/>)
/// for full compatibility with PostgreSQL <c>timestamptz</c> columns.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    private const string ExpectedStringTokenMessage = "Expected a JSON string token for DateTime.";

    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(ExpectedStringTokenMessage);
        }

        string? raw = reader.GetString();
        return UtcDateTimeParser.TryParseUtc(raw, out DateTime parsed)
            ? parsed
            : throw new JsonException(FormatInvalidDateTimeMessage(raw));
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(UtcDateTimeParser.NormalizeToUtc(value));
    }

    private static string FormatInvalidDateTimeMessage(string? raw)
        => $"The value '{raw}' is not a valid DateTime.";
}
