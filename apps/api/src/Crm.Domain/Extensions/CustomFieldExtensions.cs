using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for working with field schema collections.
/// </summary>
public static class CustomFieldExtensions
{
    /// <summary>
    /// Resolves the ordering column and data type from the field schema collection or fallback entity properties.
    /// </summary>
    public static (string OrderBy, FieldDataType? DataType) ResolveOrdering<T>(
        this IEnumerable<T>? fields,
        string? orderBy)
        where T : IFieldSchema
    {
        var customField = fields.FindFieldByOrderBy(orderBy);
        if (customField is not null)
        {
            return (customField.Key, customField.DataType);
        }

        if (!string.IsNullOrWhiteSpace(orderBy)
            && CustomRecordExtensions.SortablePropertiesMap.TryGetValue(orderBy, out var canonicalProperty))
        {
            return (canonicalProperty, null);
        }

        return (nameof(CustomRecord.Id), null);
    }

    /// <summary>
    /// Gets a field instance from the collection by the sorting string.
    /// </summary>
    public static T? FindFieldByOrderBy<T>(
        this IEnumerable<T>? fields,
        string? orderBy)
        where T : IFieldSchema
    {
        var targetKey = ExtractKey(orderBy);

        if (targetKey is null || fields is null)
        {
            return default;
        }

        foreach (var field in fields)
        {
            if (string.Equals(field.Key, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        return default;
    }

    /// <summary>
    /// Normalizes the orderBy expression by extracting the underlying JSONB field key.
    /// </summary>
    public static string? ExtractKey(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return null;
        }

        var span = orderBy.AsSpan().Trim();

        if (span.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
        {
            span = span[5..];
        }
        else if (span.StartsWith("data", StringComparison.OrdinalIgnoreCase))
        {
            int bracketIndex = span.IndexOf('[');
            if (bracketIndex >= 4 && span.EndsWith(']') && span[4..bracketIndex].Trim().IsEmpty)
            {
                span = span[(bracketIndex + 1)..^1].Trim().Trim(['\'', '\"']).Trim();
            }
        }

        if (span.IsEmpty || !char.IsAsciiLetter(span[0]))
        {
            return null;
        }

        for (int i = 1; i < span.Length; i++)
        {
            char c = span[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return null;
            }
        }

        return CustomRecordExtensions.ReservedKeys.GetAlternateLookup<ReadOnlySpan<char>>().Contains(span)
            ? null
            : span.Length == orderBy.Length ? orderBy : span.ToString();
    }

    /// <summary>
    /// Checks whether the given field key matches a reserved entity property name.
    /// </summary>
    public static bool IsReservedKey(string? key) =>
        string.IsNullOrWhiteSpace(key) || CustomRecordExtensions.ReservedKeys.Contains(key);
}
