using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Infrastructure.Persistence.Sorting;

/// <summary>
/// Builds and caches strongly-typed EF Core LINQ ordering delegates for dot-separated entity property paths
/// (e.g., <c>"Title"</c>, <c>"Topic.Title"</c>, <c>"UserProfile.IsActive"</c>) without relying on dynamic string parsing.
/// </summary>
public static class TypedPropertyOrderBuilder
{
    private static readonly ConcurrentDictionary<(Type EntityType, string Path), LambdaExpression?> KeySelectorCache =
        new(KeySelectorCacheComparer.Instance);

    /// <summary>
    /// Attempts to apply <c>OrderBy</c> / <c>OrderByDescending</c> followed by deterministic <c>ThenBy(e =&gt; e.Id)</c>
    /// for the specified dot-separated property <paramref name="propertyPath"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type implementing <see cref="IEntity"/>.</typeparam>
    /// <param name="query">The input queryable sequence.</param>
    /// <param name="propertyPath">The dot-separated property path on <typeparamref name="TEntity"/>.</param>
    /// <param name="descending">Whether to sort in descending order.</param>
    /// <param name="orderedQuery">When successful, receives the ordered queryable sequence.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="propertyPath"/> resolved to a valid public property chain;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryApplyOrdering<TEntity>(
        IQueryable<TEntity> query,
        string propertyPath,
        bool descending,
        out IQueryable<TEntity> orderedQuery)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(propertyPath))
        {
            orderedQuery = query;
            return false;
        }

        LambdaExpression? selector = KeySelectorCache.GetOrAdd(
            (typeof(TEntity), propertyPath),
            static key => BuildKeySelector(key.EntityType, key.Path));

        if (selector is null)
        {
            orderedQuery = query;
            return false;
        }

        string orderMethod = descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
        Expression orderCall = Expression.Call(
            typeof(Queryable),
            orderMethod,
            [typeof(TEntity), selector.ReturnType],
            query.Expression,
            Expression.Quote(selector));

        Expression<Func<TEntity, Guid>> idSelector = static e => e.Id;
        Expression thenByIdCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.ThenBy),
            [typeof(TEntity), typeof(Guid)],
            orderCall,
            Expression.Quote(idSelector));

        orderedQuery = query.Provider.CreateQuery<TEntity>(thenByIdCall);
        return true;
    }

    private static LambdaExpression? BuildKeySelector(Type entityType, string propertyPath)
    {
        string[] segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        ParameterExpression parameter = Expression.Parameter(entityType, "e");
        Expression current = parameter;
        Type currentType = entityType;

        foreach (string segment in segments)
        {
            PropertyInfo? property = currentType.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null || !property.CanRead)
            {
                return null;
            }

            current = Expression.Property(current, property);
            currentType = property.PropertyType;
        }

        return Expression.Lambda(current, parameter);
    }

    private sealed class KeySelectorCacheComparer : IEqualityComparer<(Type EntityType, string Path)>
    {
        public static readonly KeySelectorCacheComparer Instance = new();

        public bool Equals((Type EntityType, string Path) x, (Type EntityType, string Path) y)
            => x.EntityType == y.EntityType && string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Type EntityType, string Path) obj)
            => HashCode.Combine(obj.EntityType, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path));
    }
}
