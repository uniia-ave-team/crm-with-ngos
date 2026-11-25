using System.Collections.Frozen;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Entities;

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
        => fields.FindFieldByKey(ExtractKey(orderBy));

    /// <summary>
    /// Finds a field definition in <paramref name="fields"/> matching <paramref name="targetKey"/> (case-insensitive).
    /// </summary>
    /// <typeparam name="T">The field schema type.</typeparam>
    /// <param name="fields">The collection of field schema definitions.</param>
    /// <param name="targetKey">The extracted field key to look up.</param>
    /// <returns>The matching field definition if found; otherwise, <see langword="default"/>.</returns>
    public static T? FindFieldByKey<T>(
        this IEnumerable<T>? fields,
        string? targetKey)
        where T : IFieldSchema
    {
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
    /// Attempts to strip a container prefix using dot notation (<c>prefix.key</c>) or indexer notation
    /// (<c>prefix['key']</c>, <c>prefix["key"]</c>).
    /// </summary>
    /// <param name="input">The raw sort expression span.</param>
    /// <param name="prefix">The container prefix to match (case-insensitive).</param>
    /// <param name="extracted">When matched, receives the extracted inner key span.</param>
    /// <returns><see langword="true"/> if the container prefix was matched and stripped; otherwise, <see langword="false"/>.</returns>
    public static bool TryStripContainerPrefix(
        ReadOnlySpan<char> input,
        ReadOnlySpan<char> prefix,
        out ReadOnlySpan<char> extracted)
    {
        var span = input.Trim();

        if (span.Length > prefix.Length
            && span.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && span[prefix.Length] == '.')
        {
            extracted = span[(prefix.Length + 1)..];
            return true;
        }

        if (span.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            int bracketIndex = span.IndexOf('[');
            if (bracketIndex >= prefix.Length
                && span.EndsWith(']')
                && span[prefix.Length..bracketIndex].Trim().IsEmpty)
            {
                extracted = span[(bracketIndex + 1)..^1].Trim().Trim(['\'', '\"']).Trim();
                return true;
            }
        }

        extracted = default;
        return false;
    }

    /// <summary>
    /// Normalizes the orderBy expression by extracting the underlying JSONB field key for <see cref="CustomRecord"/>.
    /// </summary>
    /// <param name="orderBy">The raw order-by expression.</param>
    /// <returns>The extracted JSONB key, or <see langword="null"/> if invalid or reserved.</returns>
    public static string? ExtractKey(string? orderBy)
        => ExtractKey(orderBy, nameof(CustomRecord.Data), CustomRecordExtensions.ReservedKeys);

    /// <summary>
    /// Normalizes a dynamic JSONB sort expression by stripping an optional container prefix
    /// (dot or indexer notation), validating ASCII <c>snake_case</c> key characters and max length,
    /// and rejecting reserved property names.
    /// </summary>
    /// <param name="orderBy">The raw sort expression.</param>
    /// <param name="containerPrefix">The optional container prefix (e.g., <c>data</c> or <c>customFields</c>).</param>
    /// <param name="reservedKeys">The case-insensitive set of reserved entity/DTO property names.</param>
    /// <returns>The extracted JSONB key, or <see langword="null"/> if invalid, too long, or reserved.</returns>
    public static string? ExtractKey(
        string? orderBy,
        ReadOnlySpan<char> containerPrefix,
        FrozenSet<string> reservedKeys)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return null;
        }

        var span = TryStripContainerPrefix(orderBy.AsSpan(), containerPrefix, out var stripped)
            ? stripped
            : orderBy.AsSpan().Trim();

        if (span.IsEmpty
            || span.Length > FieldSchemaValidationConstants.MaxKeyLength
            || !char.IsAsciiLetter(span[0]))
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

        return reservedKeys.GetAlternateLookup<ReadOnlySpan<char>>().Contains(span)
            ? null
            : span.Length == orderBy.Length ? orderBy : span.ToString();
    }

    /// <summary>
    /// Checks whether the given field key matches a reserved entity property name.
    /// </summary>
    public static bool IsReservedKey(string? key) =>
        string.IsNullOrWhiteSpace(key) || CustomRecordExtensions.ReservedKeys.Contains(key);
}
