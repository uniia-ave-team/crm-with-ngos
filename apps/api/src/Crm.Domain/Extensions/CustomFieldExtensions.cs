using Crm.Domain.Entities;
using Crm.Domain.Entities.Base;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for resolving dynamic ordering targets against <see cref="FieldDefinitionBase"/> collections
/// using field identifiers (<see cref="BaseEntity.Id"/>) as JSONB storage keys.
/// </summary>
public static class CustomFieldExtensions
{
    /// <summary>
    /// Resolves the ordering target from the table's field schema (by field <see cref="BaseEntity.Id"/>) or scalar <see cref="CustomRecord"/> properties.
    /// Returns <see langword="null"/> when <paramref name="orderBy"/> is omitted so manual row position ordering is used by default.
    /// </summary>
    /// <typeparam name="T">The persisted field definition type.</typeparam>
    /// <param name="fields">The table's persisted field schema definitions.</param>
    /// <param name="orderBy">The requested field ID or scalar entity property name.</param>
    /// <returns>
    /// <see langword="null"/> when <paramref name="orderBy"/> is blank; otherwise the canonical lowercase field ID string,
    /// canonical scalar property name, or <see cref="BaseEntity.Id"/> fallback.
    /// </returns>
    public static string? ResolveOrdering<T>(
        this IEnumerable<T>? fields,
        string? orderBy)
        where T : FieldDefinitionBase
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return null;
        }

        string trimmed = orderBy.Trim();
        var customField = fields.FindFieldById(trimmed);

        return customField is not null
            ? customField.Id.ToString()
            : CustomRecordExtensions.SortablePropertiesMap.TryGetValue(trimmed, out var canonicalProperty)
                ? canonicalProperty
                : nameof(CustomRecord.Id);
    }

    /// <summary>
    /// Finds a field definition in <paramref name="fields"/> whose <see cref="BaseEntity.Id"/> matches <paramref name="targetFieldId"/>.
    /// </summary>
    /// <typeparam name="T">The persisted field definition type.</typeparam>
    /// <param name="fields">The collection of field definitions.</param>
    /// <param name="targetFieldId">The candidate GUID string representing the field identifier.</param>
    /// <returns>The matching field definition if found; otherwise, <see langword="default"/>.</returns>
    public static T? FindFieldById<T>(
        this IEnumerable<T>? fields,
        string? targetFieldId)
        where T : FieldDefinitionBase
    {
        if (fields is null || !TryParseFieldId(targetFieldId, out Guid parsedId))
        {
            return default;
        }

        foreach (var field in fields)
        {
            if (field.Id == parsedId)
            {
                return field;
            }
        }

        return default;
    }

    /// <summary>
    /// Validates that <paramref name="value"/> is a non-empty GUID string suitable as a dynamic JSONB field key.
    /// </summary>
    /// <param name="value">The candidate field identifier string.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> parses to a non-empty <see cref="Guid"/>; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidFieldId(string? value)
        => TryParseFieldId(value, out _);

    /// <summary>
    /// Attempts to parse <paramref name="value"/> as a non-empty <see cref="Guid"/> and returns its canonical lowercase string form.
    /// </summary>
    /// <param name="value">The candidate field identifier string.</param>
    /// <param name="normalizedId">When successful, the canonical lowercase 36-character GUID string matching JSONB keys.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryNormalizeFieldId(string? value, out string normalizedId)
    {
        if (TryParseFieldId(value, out Guid parsedId))
        {
            normalizedId = parsedId.ToString();
            return true;
        }

        normalizedId = string.Empty;
        return false;
    }

    private static bool TryParseFieldId(string? value, out Guid parsedId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsedId = Guid.Empty;
            return false;
        }

        return Guid.TryParse(value.AsSpan().Trim(), out parsedId) && parsedId != Guid.Empty;
    }
}
