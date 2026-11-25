using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="FieldDataType"/> enum.
/// </summary>
public static partial class FieldDataTypeExtensions
{
    /// <summary>
    /// Converts a field data type to its corresponding JSON string literal for backfilling.
    /// </summary>
    /// <param name="type">The field data type to convert.</param>
    /// <returns>A JSON-formatted string representation of the default backfill value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="type"/> has no defined backfill value.
    /// </exception>
    public static string GetBackfillJson(this FieldDataType type) => type switch
    {
        // TODO: Instead we should use default value from the client
        FieldDataType.Text => "\"\"",
        FieldDataType.Number => "0",
        FieldDataType.Boolean => "false",
        FieldDataType.Date => "\"1970-01-01\"",
        FieldDataType.DateTime => $"\"{DateTimeOffset.UnixEpoch:O}\"",
        FieldDataType.Email => "\"no-reply@example.com\"",
        FieldDataType.Url => "\"https://example.com\"",
        FieldDataType.PhoneNumber => "\"+100000000000\"",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            $"No backfill value defined for data type '{type}'."),
    };

    /// <summary>
    /// Validates the provided <see cref="JsonNode"/> against the <see cref="FieldDataType"/> and required status.
    /// </summary>
    /// <param name="dataType">The expected field data type.</param>
    /// <param name="fieldKey">The key of the field being validated (for exception messaging).</param>
    /// <param name="node">The JSON node value to validate.</param>
    /// <param name="isRequired">Indicates whether the field is mandatory.</param>
    /// <exception cref="InvalidCustomRecordPayloadException">Thrown when a required value is missing or data type validation fails.</exception>
    public static void ValidateJsonValue(this FieldDataType dataType, string fieldKey, JsonNode? node, bool isRequired)
    {
        if (IsNull(node))
        {
            if (isRequired)
            {
                throw new InvalidCustomRecordPayloadException($"Field '{fieldKey}' is required and cannot be null.");
            }

            return;
        }

        if (!dataType.IsValidJsonValue(node))
        {
            throw new InvalidCustomRecordPayloadException(
                $"Field '{fieldKey}' has an invalid value type. Expected '{dataType}', but received '{node!.GetValueKind()}'.");
        }
    }

    /// <summary>
    /// Attempts to parse an ISO 8601 round-trip date/time string and normalize it to UTC (<c>"O"</c> format).
    /// </summary>
    /// <param name="value">The raw ISO 8601 date/time string.</param>
    /// <param name="normalized">When successful, receives the UTC-normalized ISO 8601 string.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a valid ISO 8601 round-trip timestamp; otherwise, <see langword="false"/>.</returns>
    public static bool TryNormalizeDateTimeString(string value, out string normalized)
    {
        if (TryParseDateTimeString(value, out var parsedOffset))
        {
            normalized = parsedOffset.ToUtc().ToString("O", CultureInfo.InvariantCulture);
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static bool IsValidJsonValue(this FieldDataType dataType, JsonNode? node)
    {
        if (IsNull(node))
        {
            return true;
        }

        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        var kind = jsonValue.GetValueKind();

        return dataType switch
        {
            FieldDataType.Text => kind == JsonValueKind.String,

            FieldDataType.Number => kind == JsonValueKind.Number,

            FieldDataType.Boolean => kind is JsonValueKind.True or JsonValueKind.False,

            FieldDataType.Date => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var strDate)
                && IsValidDateString(strDate),

            FieldDataType.DateTime => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var strDateTime)
                && TryParseDateTimeString(strDateTime, out _),

            FieldDataType.Email => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var emailStr)
                && IsValidEmailString(emailStr),

            FieldDataType.Url => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var urlStr)
                && IsValidUrlString(urlStr),

            FieldDataType.PhoneNumber => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var phoneStr)
                && IsValidPhoneNumberString(phoneStr),

            _ => false,
        };
    }

    private static bool IsValidDateString(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool TryParseDateTimeString(string value, out DateTimeOffset parsedOffset)
        => DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsedOffset);

    private static bool IsValidEmailString(string value)
        => MailAddress.TryCreate(value, out var mail) && mail.Address == value;

    private static bool IsValidUrlString(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsValidPhoneNumberString(string value)
        => E164Regex().IsMatch(value);

    private static bool IsNull(JsonNode? node) => node is null || node.GetValueKind() == JsonValueKind.Null;

    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}\z", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();
}
