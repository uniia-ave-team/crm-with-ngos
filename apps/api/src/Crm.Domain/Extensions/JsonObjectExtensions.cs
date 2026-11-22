using System.Globalization;
using System.Text.Json.Nodes;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for <see cref="JsonObject"/> schema validation, null backfilling, and key reordering.
/// </summary>
public static class JsonObjectExtensions
{
    /// <summary>
    /// Validates field data types and requirements against the table schema, prevents unknown properties,
    /// and populates null values for missing optional fields. Intended for write operations (Create/Update).
    /// </summary>
    /// <param name="incomingData">The source JSON payload object.</param>
    /// <param name="customFields">The list of custom field definitions.</param>
    /// <returns>A validated and normalized <see cref="JsonObject"/> payload containing schema-only keys.</returns>
    /// <exception cref="InvalidCustomRecordPayloadException">Thrown when any field value fails data type or required validation, or when unknown fields are present.</exception>
    public static JsonObject ValidateAndBackfillPayload(this JsonObject incomingData, IReadOnlyList<CustomField> customFields)
    {
        ArgumentNullException.ThrowIfNull(incomingData);
        ArgumentNullException.ThrowIfNull(customFields);

        var allowedKeys = new HashSet<string>(customFields.Count, StringComparer.Ordinal);
        for (var i = 0; i < customFields.Count; i++)
        {
            allowedKeys.Add(customFields[i].Key);
        }

        List<string>? unknownKeys = null;
        foreach (var (key, _) in incomingData)
        {
            if (!allowedKeys.Contains(key))
            {
                unknownKeys ??= [];
                unknownKeys.Add(key);
            }
        }

        if (unknownKeys is not null && unknownKeys.Count > 0)
        {
            throw new InvalidCustomRecordPayloadException($"Payload contains unknown field(s): '{string.Join("', '", unknownKeys)}'.");
        }

        var alignedData = new JsonObject();

        for (var i = 0; i < customFields.Count; i++)
        {
            var field = customFields[i];

            if (incomingData.TryGetPropertyValue(field.Key, out var valueNode))
            {
                field.DataType.ValidateJsonValue(field.Key, valueNode, field.IsRequired);
                alignedData.Add(field.Key, NormalizeValidatedValue(field.DataType, valueNode));
            }
            else if (field.IsRequired)
            {
                throw new InvalidCustomRecordPayloadException($"Field '{field.Key}' is required.");
            }
            else
            {
                alignedData.Add(field.Key, null);
            }
        }

        return alignedData;
    }

    /// <summary>
    /// Constructs a new <see cref="JsonObject"/> with keys strictly ordered according to the provided sequence of schema keys.
    /// Moves nodes directly from <paramref name="payload"/> without performing DeepClone and explicitly populates missing schema keys with <see langword="null"/>.
    /// Intended for read operations before sending payloads to callers.
    /// </summary>
    /// <param name="payload">The source JSON payload object retrieved from storage.</param>
    /// <param name="orderedKeys">The pre-ordered list or array of schema keys.</param>
    /// <returns>A new <see cref="JsonObject"/> with properties ordered according to <paramref name="orderedKeys"/>, with missing keys explicitly set to <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> or <paramref name="orderedKeys"/> is <see langword="null"/>.</exception>
    public static JsonObject ReorderKeys(
        this JsonObject payload,
        IReadOnlyList<string> orderedKeys)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(orderedKeys);

        var reorderedData = new JsonObject();

        for (var i = 0; i < orderedKeys.Count; i++)
        {
            var key = orderedKeys[i];
            payload.Remove(key, out var node);
            reorderedData.Add(key, node);
        }

        return reorderedData;
    }

    private static JsonNode? NormalizeValidatedValue(FieldDataType dataType, JsonNode? valueNode)
        => valueNode switch
        {
            null => null,
            JsonValue jsonValue when dataType == FieldDataType.DateTime
                && jsonValue.TryGetValue<string>(out var rawDateTime)
                && DateTimeOffset.TryParseExact(
                    rawDateTime,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedOffset)
                => JsonValue.Create(parsedOffset.ToUtc().ToString("O", CultureInfo.InvariantCulture)),
            _ => valueNode.DeepClone(),
        };
}
