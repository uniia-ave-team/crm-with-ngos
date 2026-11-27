using System.Collections.Frozen;
using Crm.Domain.Constants;
using Crm.Domain.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides built-in user sort validation, custom-field sort resolution, and visibility key partitioning
/// for <see cref="UserCustomField"/> using field identifiers (<see cref="Entities.Base.BaseEntity.Id"/>) as JSONB keys.
/// </summary>
public static class UserCustomFieldExtensions
{
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
    /// Checks whether <paramref name="orderBy"/> matches one of the built-in user sort fields.
    /// </summary>
    /// <param name="orderBy">The candidate sort field name.</param>
    /// <returns><see langword="true"/> if <paramref name="orderBy"/> is a built-in user sort field; otherwise, <see langword="false"/>.</returns>
    public static bool IsBuiltInSortField(string? orderBy)
        => !string.IsNullOrWhiteSpace(orderBy) && BuiltInSortFields.Contains(orderBy.Trim());

    /// <summary>
    /// Checks whether <paramref name="orderBy"/> is either a valid built-in sort field or a non-empty custom field GUID.
    /// </summary>
    /// <param name="orderBy">The raw <c>OrderBy</c> value.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowedUserOrderBy(string? orderBy)
        => orderBy is null
            || IsBuiltInSortField(orderBy)
            || CustomFieldExtensions.IsValidFieldId(orderBy);

    /// <summary>
    /// Resolves a user <paramref name="orderBy"/> field ID against the caller's visible custom field schema,
    /// returning the canonical lowercase GUID key when matched.
    /// </summary>
    /// <param name="fields">The visible user custom field schema definitions.</param>
    /// <param name="orderBy">The requested sort field ID.</param>
    /// <returns>The canonical lowercase field ID string if a visible matching column exists; otherwise, <see langword="null"/>.</returns>
    public static string? ResolveOrdering(
        this IEnumerable<UserCustomField>? fields,
        string? orderBy)
        => fields.FindFieldById(orderBy)?.Id.ToString();

    /// <summary>
    /// Splits ordered <see cref="UserCustomField"/> schema definitions into all ordered field ID keys,
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
            string key = field.Id.ToString();
            allKeys[i] = key;

            if (field.IsPublic)
            {
                publicKeys.Add(key);
            }
            else
            {
                privateKeys.Add(key);
            }
        }

        return (allKeys, [.. publicKeys], [.. privateKeys]);
    }
}
