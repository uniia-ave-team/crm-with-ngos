using System.Linq.Expressions;
using Crm.Domain.Constants;
using Crm.Infrastructure.Persistence.Extensions;
using LinqKit;

namespace Crm.Infrastructure.Persistence.Sorting;

/// <summary>
/// Provides reusable sort-direction parsing and language-agnostic smart NULLS-LAST dynamic ordering helpers
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
    /// Applies language-agnostic smart NULLS-LAST ordering over a scalar text expression (such as <c>jsonb_extract_path_text</c>):
    /// <list type="bullet">
    /// <item><description>For typed columns (<see cref="InferredSortKind.Number"/>, <see cref="InferredSortKind.Boolean"/>, <see cref="InferredSortKind.Date"/>, <see cref="InferredSortKind.DateTime"/>): valid parsed values sort accurately first via <see cref="PostgresCastFunctions"/>, followed by unparseable text values in default string order, with <see langword="null"/> always last.</description></item>
    /// <item><description>For <see cref="InferredSortKind.Text"/>: values sort using default database string comparison with <see langword="null"/> always last.</description></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="query">The input queryable sequence.</param>
    /// <param name="textSelector">The SQL-translatable expression extracting the raw text value.</param>
    /// <param name="sortKind">The inferred sort kind for the column.</param>
    /// <param name="idSelector">The deterministic tie-breaker identifier selector.</param>
    /// <param name="isDescending">Whether to sort non-null values in descending order.</param>
    /// <returns>The ordered queryable sequence.</returns>
    public static IQueryable<TEntity> ApplySmartNullsLastOrdering<TEntity>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, string?>> textSelector,
        InferredSortKind sortKind,
        Expression<Func<TEntity, Guid>> idSelector,
        bool isDescending)
        => sortKind switch
        {
            InferredSortKind.Number => query.ApplyHybridCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastNumber(v), idSelector, isDescending),
            InferredSortKind.Boolean => query.ApplyHybridCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastBoolean(v), idSelector, isDescending),
            InferredSortKind.Date => query.ApplyHybridCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastDate(v), idSelector, isDescending),
            InferredSortKind.DateTime => query.ApplyHybridCastOrdering(textSelector, v => PostgresCastFunctions.SafeCastDateTime(v), idSelector, isDescending),
            InferredSortKind.Text or _ => query.ApplyNullsLastOrdering(textSelector, idSelector, isDescending),
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

    private static IQueryable<TEntity> ApplyHybridCastOrdering<TEntity, TProp>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, string?>> textSelector,
        Expression<Func<string?, TProp>> castSelector,
        Expression<Func<TEntity, Guid>> idSelector,
        bool isDescending)
    {
        Expression<Func<TEntity, bool>> isTextNull = e => textSelector.Invoke(e) == null;
        Expression<Func<TEntity, TProp>> castKey = e => castSelector.Invoke(textSelector.Invoke(e));
        Expression<Func<TEntity, bool>> isCastNull = e => castSelector.Invoke(textSelector.Invoke(e)) == null;

        IOrderedQueryable<TEntity> ordered = query
            .OrderBy(isTextNull.Expand())
            .ThenBy(isCastNull.Expand());

        ordered = isDescending
            ? ordered.ThenByDescending(castKey.Expand()).ThenByDescending(textSelector)
            : ordered.ThenBy(castKey.Expand()).ThenBy(textSelector);

        return ordered.ThenBy(idSelector);
    }
}
