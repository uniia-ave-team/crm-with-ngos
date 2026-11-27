using System.Linq.Expressions;
using LinqKit;
using Mapster;
using Mapster.Models;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Expressions;

/// <summary>
/// Builds and executes reusable SQL-translatable <see cref="KeyValuePair{TKey, TValue}"/> Mapster projections
/// powered by <c>LinqKit</c> for batch lookup repository queries (such as fetching roles or custom fields grouped by user identifier).
/// </summary>
internal static class MapsterLookupProjectionBuilder
{
    /// <summary>
    /// Executes <paramref name="query"/> using a SQL-side Mapster projection into a lookup grouped by <paramref name="keySelector"/>,
    /// falling back to in-memory <c>Adapt&lt;TResult&gt;()</c> when projection is unavailable or unnecessary.
    /// </summary>
    /// <typeparam name="TSource">The queried EF entity type.</typeparam>
    /// <typeparam name="TTarget">The source type for the Mapster projection.</typeparam>
    /// <typeparam name="TKey">The lookup grouping key type.</typeparam>
    /// <typeparam name="TResult">The target projected DTO type.</typeparam>
    /// <param name="query">The filtered (and optionally ordered) queryable sequence.</param>
    /// <param name="keySelector">An expression selecting the grouping key from <typeparamref name="TSource"/>.</param>
    /// <param name="targetSelector">An expression selecting the Mapster source object from <typeparamref name="TSource"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A lookup grouping projected <typeparamref name="TResult"/> values by <typeparamref name="TKey"/>.</returns>
    public static async Task<ILookup<TKey, TResult>> ToProjectedLookupAsync<TSource, TTarget, TKey, TResult>(
        this IQueryable<TSource> query,
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TTarget>> targetSelector,
        CancellationToken cancellationToken = default)
    {
        var projection = BuildKeyValueProjection<TSource, TTarget, TKey, TResult>(keySelector, targetSelector);
        if (projection is not null)
        {
            var projectedPairs = await query.Select(projection).ToListAsync(cancellationToken);
            return projectedPairs.ToLookup(static pair => pair.Key, static pair => pair.Value);
        }

        Expression<Func<TSource, KeyValuePair<TKey, TTarget>>> rawPairSelector = source =>
            new KeyValuePair<TKey, TTarget>(keySelector.Invoke(source), targetSelector.Invoke(source));

        var rawPairs = await query.Select(rawPairSelector.Expand()).ToListAsync(cancellationToken);
        return rawPairs.ToLookup(
            static pair => pair.Key,
            static pair => pair.Value is TResult direct ? direct : pair.Value!.Adapt<TResult>());
    }

    /// <summary>
    /// Builds a projection expression mapping each <typeparamref name="TSource"/> row to a
    /// <see cref="KeyValuePair{TKey, TResult}"/> whose value is projected from <typeparamref name="TTarget"/> via Mapster.
    /// Returns <see langword="null"/> when <typeparamref name="TResult"/> equals <typeparamref name="TTarget"/> or
    /// Mapster cannot generate a projection expression.
    /// </summary>
    /// <typeparam name="TSource">The queried EF entity type.</typeparam>
    /// <typeparam name="TTarget">The source type for the Mapster projection (same as <typeparamref name="TSource"/> or a navigation property).</typeparam>
    /// <typeparam name="TKey">The lookup grouping key type.</typeparam>
    /// <typeparam name="TResult">The target projected DTO type.</typeparam>
    /// <param name="keySelector">An expression selecting the grouping key from <typeparamref name="TSource"/>.</param>
    /// <param name="targetSelector">An expression selecting the Mapster source object from <typeparamref name="TSource"/>.</param>
    /// <returns>A SQL-translatable projection expression, or <see langword="null"/>.</returns>
    public static Expression<Func<TSource, KeyValuePair<TKey, TResult>>>? BuildKeyValueProjection<TSource, TTarget, TKey, TResult>(
        Expression<Func<TSource, TKey>> keySelector,
        Expression<Func<TSource, TTarget>> targetSelector)
    {
        if (typeof(TResult) == typeof(TTarget))
        {
            return null;
        }

        try
        {
            if (TypeAdapterConfig.GlobalSettings.CreateMapExpression(
                new TypeTuple(typeof(TTarget), typeof(TResult)),
                MapType.Projection) is not Expression<Func<TTarget, TResult>> mapExpression)
            {
                return null;
            }

            Expression<Func<TSource, KeyValuePair<TKey, TResult>>> projection = source =>
                new KeyValuePair<TKey, TResult>(
                    keySelector.Invoke(source),
                    mapExpression.Invoke(targetSelector.Invoke(source)));

            return projection.Expand();
        }
        catch
        {
            return null;
        }
    }
}
