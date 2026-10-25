using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Crm.Domain.Enums;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="FieldDataType"/> enum.
/// </summary>
public static partial class FieldDataTypeExtensions
{
    /// <summary>
    /// Resolves the default <see cref="JsonNode"/> value for a field data type based on its requirement status.
    /// </summary>
    /// <param name="dataType">The field data type.</param>
    /// <returns>A default <see cref="JsonNode"/> representation or <c>null</c> if the field is optional.</returns>
    public static JsonNode? GetBackfillValue(this FieldDataType dataType)
    {
        // TODO: Instead we should use default value from the client
        return dataType switch
        {
            FieldDataType.Text => JsonValue.Create(string.Empty),
            FieldDataType.Number => JsonValue.Create(0),
            FieldDataType.Boolean => JsonValue.Create(false),
            FieldDataType.Date => JsonValue.Create("1970-01-01"),
            FieldDataType.DateTime => JsonValue.Create(DateTimeOffset.UnixEpoch.ToString("O")),
            FieldDataType.Email => JsonValue.Create("no-reply@example.com"),
            FieldDataType.Url => JsonValue.Create("https://example.com"),
            FieldDataType.PhoneNumber => JsonValue.Create("+100000000000"),
            _ => null,
        };
    }

    /// <summary>
    /// Validates whether the provided <see cref="JsonNode"/> matches the expected <see cref="FieldDataType"/>.
    /// </summary>
    /// <param name="dataType">The expected field data type.</param>
    /// <param name="node">The JSON node value to validate.</param>
    /// <returns><c>true</c> if the node is valid for the data type or null; otherwise, <c>false</c>.</returns>
    public static bool IsValidJsonValue(this FieldDataType dataType, JsonNode? node)
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
                && DateOnly.TryParseExact(strDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),

            FieldDataType.DateTime => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var strDateTime)
                && DateTimeOffset.TryParseExact(strDateTime, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),

            FieldDataType.Email => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var emailStr)
                && MailAddress.TryCreate(emailStr, out var mail)
                && mail.Address == emailStr,

            FieldDataType.Url => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var urlStr)
                && Uri.TryCreate(urlStr, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),

            FieldDataType.PhoneNumber => kind == JsonValueKind.String
                && jsonValue.TryGetValue<string>(out var phoneStr)
                && E164Regex().IsMatch(phoneStr),

            _ => false,
        };
    }

    /// <summary>
    /// Validates the provided <see cref="JsonNode"/> against the <see cref="FieldDataType"/> and required status.
    /// </summary>
    /// <param name="dataType">The expected field data type.</param>
    /// <param name="fieldKey">The key of the field being validated (for exception messaging).</param>
    /// <param name="node">The JSON node value to validate.</param>
    /// <param name="isRequired">Indicates whether the field is mandatory.</param>
    /// <exception cref="ArgumentException">Thrown when a required value is missing or data type validation fails.</exception>
    public static void ValidateJsonValue(this FieldDataType dataType, string fieldKey, JsonNode? node, bool isRequired)
    {
        if (IsNull(node))
        {
            if (isRequired)
            {
                throw new ArgumentException($"Field '{fieldKey}' is required and cannot be null.", fieldKey);
            }

            return;
        }

        if (!dataType.IsValidJsonValue(node))
        {
            throw new ArgumentException(
                $"Field '{fieldKey}' has an invalid value type. Expected '{dataType}', but received '{node.GetValueKind()}'.",
                fieldKey);
        }
    }

    private static bool IsNull(JsonNode? node) => node is null || node.GetValueKind() == JsonValueKind.Null;

    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}\z", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();
}
