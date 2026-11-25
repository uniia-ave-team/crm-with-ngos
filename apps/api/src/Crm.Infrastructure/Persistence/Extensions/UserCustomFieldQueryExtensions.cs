using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Expressions;
using Crm.Infrastructure.Persistence.Sorting;
using LinqKit;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides SQL-translatable extension methods for searching and dynamically sorting <see cref="AuthUser"/>
/// queries by JSONB <see cref="Domain.Entities.User.CustomFields"/> values, reusing
/// <see cref="JsonbFunctionsExtensions.JsonbExtractPathText"/>, <see cref="QueryFilterExpressions"/>,
/// and <see cref="TypedOrderingExtensions.ApplyTypedNullsLastOrdering{TEntity}"/> (with zero reflection).
/// </summary>
public static class UserCustomFieldQueryExtensions
{
    private static readonly Expression<Func<AuthUser, Guid>> IdSelector = u => u.Id;

    /// <summary>
    /// Builds a SQL-translatable predicate matching users whose visible JSONB custom field values
    /// contain <paramref name="searchTerm"/> (case-insensitive via <c>ILIKE</c>).
    /// Public keys are searched across all users, while private keys are searched only on <paramref name="currentUserId"/>.
    /// </summary>
    /// <param name="searchTerm">The non-empty search keyword.</param>
    /// <param name="publicKeys">Schema keys searchable across all users (all keys when the caller has global custom field visibility).</param>
    /// <param name="privateKeys">Private schema keys searchable only on the caller's own row.</param>
    /// <param name="currentUserId">The optional requesting user identifier for self-visibility checks.</param>
    /// <returns>An expression matching users with a matching visible JSONB custom field value, or <see langword="null"/> when no searchable keys exist.</returns>
    public static Expression<Func<AuthUser, bool>>? BuildVisibleCustomFieldSearchPredicate(
        string searchTerm,
        IReadOnlyList<string>? publicKeys,
        IReadOnlyList<string>? privateKeys,
        Guid? currentUserId)
    {
        Expression<Func<AuthUser, bool>>? publicPredicate = BuildKeysMatchPredicate(searchTerm, publicKeys);
        Expression<Func<AuthUser, bool>>? privatePredicate = currentUserId is { } callerId && callerId != Guid.Empty
            ? BuildPrivateSelfMatchPredicate(searchTerm, privateKeys, callerId)
            : null;

        return (publicPredicate, privatePredicate) switch
        {
            (null, null) => null,
            (not null, null) => publicPredicate,
            (null, not null) => privatePredicate,
            (not null, not null) => publicPredicate.Or(privatePredicate).Expand(),
        };
    }

    /// <summary>
    /// Applies dynamic NULLS-LAST sorting on a user JSONB custom field using safe PostgreSQL typed casting
    /// (identical to <see cref="CustomRecordQueryExtensions.OrderByCustomRecord"/>).
    /// </summary>
    /// <param name="query">The input user queryable sequence.</param>
    /// <param name="sort">The resolved visible custom field key and data type.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
    /// <returns>The ordered user queryable sequence.</returns>
    public static IQueryable<AuthUser> OrderByUserCustomField(
        this IQueryable<AuthUser> query,
        UserCustomFieldSort sort,
        string? sortOrder)
    {
        string key = sort.Key;

        return query.ApplyTypedNullsLastOrdering(
            u => JsonbFunctionsExtensions.JsonbExtractPathText(u.UserProfile.CustomFields, key),
            sort.DataType,
            IdSelector,
            sortOrder.IsDescending());
    }

    private static Expression<Func<AuthUser, bool>>? BuildPrivateSelfMatchPredicate(
        string searchTerm,
        IReadOnlyList<string>? privateKeys,
        Guid callerId)
    {
        Expression<Func<AuthUser, bool>>? privateKeyMatch = BuildKeysMatchPredicate(searchTerm, privateKeys);
        if (privateKeyMatch is null)
        {
            return null;
        }

        Expression<Func<AuthUser, bool>> isSelf = u => u.Id == callerId;
        return isSelf.And(privateKeyMatch).Expand();
    }

    private static Expression<Func<AuthUser, bool>>? BuildKeysMatchPredicate(
        string searchTerm,
        IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0)
        {
            return null;
        }

        Expression<Func<AuthUser, string?>>[] selectors =
        [
            .. keys.Select<string, Expression<Func<AuthUser, string?>>>(key =>
            {
                string capturedKey = key;
                return u => JsonbFunctionsExtensions.JsonbExtractPathText(u.UserProfile.CustomFields, capturedKey);
            }),
        ];

        return QueryFilterExpressions.ContainsIgnoreCase(searchTerm, selectors);
    }
}
