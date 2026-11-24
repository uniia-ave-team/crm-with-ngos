using Crm.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides LINQ queryable extensions for requesting database row-level write locks (<c>FOR NO KEY UPDATE</c>)
/// handled transparently by <see cref="RowLockCommandInterceptor"/>.
/// </summary>
public static class RowLockQueryableExtensions
{
    /// <summary>
    /// Marks the query to acquire a row-level <c>FOR NO KEY UPDATE</c> lock on matched rows within the active database
    /// transaction, serializing concurrent writers without blocking foreign-key child inserts.
    /// </summary>
    /// <typeparam name="T">The queried element type.</typeparam>
    /// <param name="query">The source LINQ queryable.</param>
    /// <returns>The tagged queryable that executes with <c>FOR NO KEY UPDATE</c> on relational databases.</returns>
    public static IQueryable<T> WithRowLock<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.TagWith(RowLockCommandInterceptor.ForNoKeyUpdateTag);
    }
}
