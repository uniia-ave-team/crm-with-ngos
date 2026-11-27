using System.Text.Json;
using System.Text.Json.Nodes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for <see cref="JsonObject"/> schema validation, default payload creation, null backfilling, and key reordering
/// keyed by each <see cref="FieldDefinitionBase"/> identifier (<see cref="BaseEntity.Id"/>).
/// </summary>
public static class JsonObjectExtensions
{
    /// <summary>
    /// Validates that every value is a JSON string (or <see langword="null"/> for optional fields), prevents unknown properties,
    /// and populates <see langword="null"/> values for missing optional fields. Intended for write operations (Create/Update).
    /// </summary>
    /// <param name="incomingData">The source JSON payload object.</param>
    /// <param name="customFields">The list of persisted field definitions.</param>
    /// <returns>A validated and normalized <see cref="JsonObject"/> payload containing schema-only field ID keys.</returns>
    /// <exception cref="InvalidCustomRecordPayloadException">Thrown when any field value is not a string, a required field is missing or null, or unknown fields are present.</exception>
    public static JsonObject ValidateAndBackfillPayload(this JsonObject incomingData, IReadOnlyList<FieldDefinitionBase> customFields)
    {
        ArgumentNullException.ThrowIfNull(incomingData);
        ArgumentNullException.ThrowIfNull(customFields);

        var fieldKeys = new string[customFields.Count];
        var allowedKeys = new HashSet<string>(customFields.Count, StringComparer.Ordinal);
        for (var i = 0; i < customFields.Count; i++)
        {
            string key = customFields[i].Id.ToString();
            fieldKeys[i] = key;
            allowedKeys.Add(key);
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
            string fieldKey = fieldKeys[i];

            if (incomingData.TryGetPropertyValue(fieldKey, out var valueNode))
            {
                ValidateStringOrNullValue(fieldKey, valueNode, field.IsRequired);
                alignedData.Add(fieldKey, valueNode?.DeepClone());
            }
            else if (field.IsRequired)
            {
                throw new InvalidCustomRecordPayloadException($"Field '{fieldKey}' is required.");
            }
            else
            {
                alignedData.Add(fieldKey, null);
            }
        }

        return alignedData;
    }

    /// <summary>
    /// Constructs a new <see cref="JsonObject"/> with keys strictly ordered according to the provided sequence of field ID keys.
    /// Moves nodes directly from <paramref name="payload"/> without performing DeepClone and explicitly populates missing schema keys with <see langword="null"/>.
    /// Intended for read operations before sending payloads to callers.
    /// </summary>
    /// <param name="payload">The source JSON payload object retrieved from storage.</param>
    /// <param name="orderedKeys">The pre-ordered list or array of field ID keys.</param>
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

    /// <summary>
    /// Creates an initial schema-aligned <see cref="JsonObject"/> payload for newly created entities (such as newly registered users),
    /// backfilling mandatory fields with an empty string and setting optional fields to <see langword="null"/>.
    /// </summary>
    /// <param name="fields">The ordered field schema definitions.</param>
    /// <returns>A schema-aligned <see cref="JsonObject"/> initialized with defaults.</returns>
    public static JsonObject CreateDefaultPayload(this IEnumerable<FieldDefinitionBase> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var payload = new JsonObject();
        foreach (var field in fields.OrderBy(f => f.Order))
        {
            payload[field.Id.ToString()] = field.IsRequired
                ? JsonValue.Create(string.Empty)
                : null;
        }

        return payload;
    }

    private static void ValidateStringOrNullValue(string fieldKey, JsonNode? node, bool isRequired)
    {
        if (node is null || node.GetValueKind() == JsonValueKind.Null)
        {
            if (isRequired)
            {
                throw new InvalidCustomRecordPayloadException($"Field '{fieldKey}' is required and cannot be null.");
            }

            return;
        }

        if (node is not JsonValue jsonValue || jsonValue.GetValueKind() != JsonValueKind.String)
        {
            throw new InvalidCustomRecordPayloadException(
                $"Field '{fieldKey}' has an invalid value type. Expected 'String', but received '{node.GetValueKind()}'.");
        }
    }
}
