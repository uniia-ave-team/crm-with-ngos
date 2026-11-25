using System.Linq.Expressions;
using Crm.Domain.Constants;
using Crm.Domain.Enums;
using Crm.Infrastructure.Persistence.Extensions;
using LinqKit;

namespace Crm.Infrastructure.Persistence.Sorting;

/// <summary>
/// Provides reusable sort-direction parsing and strongly-typed NULLS-LAST dynamic ordering helpers
/// powered by <c>LinqKit</c> and <see cref="PostgresCastFunctions"/> (with zero reflection).
/// Shared across custom table records, user custom fields, projects, tasks, and repositories.
/// </summary>
public static class TypedOrderingExtensions
{
    /// <summary>
    /// Determines whether <paramref name="sortOrder"/> specifies descending ordering.
    /// </summary>
    /// <param name="sortOrder">The raw sort order string (<c>"asc"</c> or <c>"desc"</c>).</param>
    /// <returns><see langword="true"/> if descending; otherwise, <see langword="false"/>.</returns>
    public static bool IsDescending(this string? sortOrder)
        => string.Equals(sortOrder?.Trim(), SortOrderConstants.Descending, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Applies strongly-typed safe PostgreSQL casting and NULLS-LAST ordering over a scalar text expression
    /// (for example, <c>jsonb_extract_path_text</c> on <see cref="Domain.Entities.CustomRecord"/> or <see cref="Domain.Entities.User"/>).
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="query">The input queryable sequence.</param>
    /// <param name="textSelector">The SQL-translatable expression extracting the raw text value.</param>
    /// <param name="dataType">The optional field data type determining the PostgreSQL cast target.</param>
    /// <param name="idSelector">The deterministic tie-breaker identifier selector.</param>
    /// <param name="isDescending">Whether to sort non-null values in descending order.</param>
    /// <returns>The ordered queryable sequence.</returns>
    public static IQueryable<TEntity> ApplyTypedNullsLastOrdering<TEntity>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, string?>> textSelector,
        FieldDataType? dataType,
        Expression<Func<TEntity, Guid>> idSelector,
        bool isDescending)
        => dataType switch
        {
            FieldDataType.Number => query.ApplyCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastNumber(v), idSelector, isDescending),
            FieldDataType.Boolean => query.ApplyCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastBoolean(v), idSelector, isDescending),
            FieldDataType.Date => query.ApplyCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastDate(v), idSelector, isDescending),
            FieldDataType.DateTime => query.ApplyCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastDateTime(v), idSelector, isDescending),
            FieldDataType.Text or
            FieldDataType.Email or
            FieldDataType.PhoneNumber or
            FieldDataType.Url or
            _ => query.ApplyNullsLastOrdering(textSelector.Expand(), idSelector, isDescending),
        };

    /// <summary>
    /// Applies ascending or descending ordering on <paramref name="keySelector"/> while ensuring
    /// <see langword="null"/> values sort last and breaking ties deterministically by <paramref name="idSelector"/>.
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <typeparam name="TProp">The nullable sort key type.</typeparam>
    /// <param name="query">The input queryable sequence.</param>
    /// <param name="keySelector">The expression projecting the nullable sort value.</param>
    /// <param name="idSelector">The deterministic tie-breaker identifier selector.</param>
    /// <param name="isDescending">Whether to sort non-null values in descending order.</param>
    /// <param name="nullsLastInAscending">
    /// When <see langword="true"/>, emits an explicit <c>key == null</c> primary sort even in ascending direction
    /// (required for LINQ-to-Objects consistency on nullable value types such as <see cref="DateTime"/>).
    /// </param>
    /// <returns>The ordered queryable sequence.</returns>
    public static IQueryable<TEntity> ApplyNullsLastOrdering<TEntity, TProp>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TProp>> keySelector,
        Expression<Func<TEntity, Guid>> idSelector,
        bool isDescending,
        bool nullsLastInAscending = false)
    {
        if (!isDescending && !nullsLastInAscending)
        {
            return query.OrderBy(keySelector).ThenBy(idSelector);
        }

        Expression<Func<TEntity, bool>> isNullSelector = e => keySelector.Invoke(e) == null;
        Expression<Func<TEntity, bool>> expandedNullCheck = isNullSelector.Expand();

        return isDescending
            ? query.OrderBy(expandedNullCheck)
                .ThenByDescending(keySelector)
                .ThenBy(idSelector)
            : query.OrderBy(expandedNullCheck)
                .ThenBy(keySelector)
                .ThenBy(idSelector);
    }

    private static IQueryable<TEntity> ApplyCastOrdering<TEntity, TProp>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, string?>> textSelector,
        Expression<Func<string?, TProp>> castSelector,
        Expression<Func<TEntity, Guid>> idSelector,
        bool isDescending)
    {
        Expression<Func<TEntity, TProp>> keySelector = e => castSelector.Invoke(textSelector.Invoke(e));
        return query.ApplyNullsLastOrdering(keySelector.Expand(), idSelector, isDescending);
    }
}
