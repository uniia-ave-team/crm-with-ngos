using System.Linq.Expressions;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides conditional query composition extension methods for <see cref="IQueryable{T}"/>.
/// </summary>
internal static class QueryableExtensions
{
    /// <summary>
    /// Conditionally applies <paramref name="predicate"/> to <paramref name="query"/> when <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <typeparam name="T">The queried entity type.</typeparam>
    /// <param name="query">The source queryable sequence.</param>
    /// <param name="condition">Whether to apply the filter.</param>
    /// <param name="predicate">The SQL-translatable filter predicate.</param>
    /// <returns>The filtered queryable when <paramref name="condition"/> is <see langword="true"/>; otherwise, the original queryable.</returns>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, bool>> predicate)
        => condition ? query.Where(predicate) : query;

    /// <summary>
    /// Conditionally constructs and applies a predicate to <paramref name="query"/> when <paramref name="condition"/> is <see langword="true"/>,
    /// avoiding expression allocation when the condition is false.
    /// </summary>
    /// <typeparam name="T">The queried entity type.</typeparam>
    /// <param name="query">The source queryable sequence.</param>
    /// <param name="condition">Whether to apply the filter.</param>
    /// <param name="predicateFactory">A factory creating the SQL-translatable filter predicate.</param>
    /// <returns>The filtered queryable when <paramref name="condition"/> is <see langword="true"/>; otherwise, the original queryable.</returns>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        Func<Expression<Func<T, bool>>> predicateFactory)
        => condition ? query.Where(predicateFactory()) : query;
}
