using System.Collections.Frozen;
using System.Reflection;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides reserved-key protection, dynamic JSONB sort-key resolution, and schema key extraction helpers
/// for <see cref="UserCustomField"/>, reusing <see cref="CustomFieldExtensions"/>.
/// </summary>
public static class UserCustomFieldExtensions
{
    /// <summary>
    /// The optional container prefix used in <c>OrderBy</c> expressions targeting user custom fields
    /// (e.g., <c>customFields.city</c>, <c>customFields['birth_date']</c>, or bare <c>city</c>).
    /// </summary>
    public const string SortPrefix = UserConstants.CustomFieldsSortPrefix;

    /// <summary>
    /// Case-insensitive set of allowed built-in user sort properties and aliases.
    /// </summary>
    public static readonly FrozenSet<string> BuiltInSortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(User.Id),
        UserConstants.Email,
        nameof(User.FirstName),
        nameof(User.LastName),
        nameof(User.Patronymic),
        nameof(User.PhoneNumber),
        nameof(User.Country),
        nameof(User.EmergencyContact),
        nameof(User.Pronouns),
        nameof(User.PronounCategory),
        nameof(User.IsActive),
        UserConstants.FullName,
        nameof(User.CreatedAt),
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Case-insensitive set of reserved user, DTO, and identity property names that cannot be used as user custom field keys.
    /// </summary>
    public static readonly FrozenSet<string> ReservedKeys;

    static UserCustomFieldExtensions()
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string key in BuiltInSortFields)
        {
            reserved.Add(key);
        }

        reserved.Add(UserConstants.AvatarUrl);
        reserved.Add(UserConstants.Roles);
        reserved.Add(UserConstants.CustomFields);

        foreach (string identityKey in UserConstants.IdentityReservedKeys)
        {
            reserved.Add(identityKey);
        }

        foreach (var prop in typeof(User).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            reserved.Add(prop.Name);
        }

        ReservedKeys = reserved.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether <paramref name="orderBy"/> matches one of the built-in user sort fields.
    /// </summary>
    /// <param name="orderBy">The candidate sort field name.</param>
    /// <returns><see langword="true"/> if <paramref name="orderBy"/> is a built-in user sort field; otherwise, <see langword="false"/>.</returns>
    public static bool IsBuiltInSortField(string? orderBy)
        => !string.IsNullOrWhiteSpace(orderBy) && BuiltInSortFields.Contains(orderBy.Trim());

    /// <summary>
    /// Checks whether the given key matches a reserved user, DTO, or identity property name.
    /// </summary>
    /// <param name="key">The candidate custom field key.</param>
    /// <returns><see langword="true"/> if the key is blank or reserved; otherwise, <see langword="false"/>.</returns>
    public static bool IsReservedKey(string? key)
        => string.IsNullOrWhiteSpace(key) || ReservedKeys.Contains(key.Trim());

    /// <summary>
    /// Checks whether <paramref name="orderBy"/> is either a valid built-in sort field or a syntactically valid custom field sort expression.
    /// </summary>
    /// <param name="orderBy">The raw <c>OrderBy</c> value.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowedUserOrderBy(string? orderBy)
        => orderBy is null
            || IsBuiltInSortField(orderBy)
            || ExtractSortKey(orderBy) is not null;

    /// <summary>
    /// Extracts the JSONB custom field key from a user <c>OrderBy</c> expression, supporting both prefixed
    /// (<c>customFields.key</c>, <c>customFields['key']</c>, <c>customFields["key"]</c>) and bare (<c>key</c>) forms,
    /// delegating directly to <see cref="CustomFieldExtensions.ExtractKey(string?, ReadOnlySpan{char}, FrozenSet{string})"/>.
    /// </summary>
    /// <param name="orderBy">The raw sort expression.</param>
    /// <returns>The extracted custom field key, or <see langword="null"/> if blank, reserved, or syntactically invalid.</returns>
    public static string? ExtractSortKey(string? orderBy)
        => CustomFieldExtensions.ExtractKey(orderBy, SortPrefix, ReservedKeys);

    /// <summary>
    /// Resolves a user <paramref name="orderBy"/> expression against the caller's visible custom field schema,
    /// returning the canonical key and <see cref="Enums.FieldDataType"/> when matched.
    /// </summary>
    /// <param name="fields">The visible user custom field schema definitions.</param>
    /// <param name="orderBy">The raw sort expression.</param>
    /// <returns>A <see cref="UserCustomFieldSort"/> if a visible matching column exists; otherwise, <see langword="null"/>.</returns>
    public static UserCustomFieldSort? ResolveOrdering(
        this IEnumerable<UserCustomField>? fields,
        string? orderBy)
        => fields.FindFieldByKey(ExtractSortKey(orderBy)) is { } field
            ? new UserCustomFieldSort(field.Key, field.DataType)
            : null;

    /// <summary>
    /// Splits ordered <see cref="UserCustomField"/> schema definitions into all ordered keys,
    /// public-only keys, and private-only keys for response projection, privacy filtering, and SQL search.
    /// </summary>
    /// <param name="orderedFields">The schema definitions already ordered by <see cref="Entities.Base.FieldDefinitionBase.Order"/>.</param>
    /// <returns>A tuple containing all keys, public keys, and private keys in schema order.</returns>
    public static (string[] AllKeys, string[] PublicKeys, string[] PrivateKeys) PartitionSchemaKeys(
        this IReadOnlyList<UserCustomField> orderedFields)
    {
        if (orderedFields.Count == 0)
        {
            return ([], [], []);
        }

        var allKeys = new string[orderedFields.Count];
        List<string> publicKeys = [];
        List<string> privateKeys = [];

        for (int i = 0; i < orderedFields.Count; i++)
        {
            var field = orderedFields[i];
            allKeys[i] = field.Key;

            if (field.IsPublic)
            {
                publicKeys.Add(field.Key);
            }
            else
            {
                privateKeys.Add(field.Key);
            }
        }

        return (allKeys, [.. publicKeys], [.. privateKeys]);
    }
}
