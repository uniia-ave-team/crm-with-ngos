using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using Crm.Domain.Consts;
using Crm.Domain.Consts.Entities.GeneralConstants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Persistence.Extensions.FunctionExtensions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides extension methods for querying, filtering, and dynamically ordering <see cref="CustomRecord"/> sequences.
/// </summary>
public static class CustomRecordQueryExtensions
{
    private static readonly ConcurrentDictionary<(string PropertyName, bool IsDescending),
        Func<IQueryable<CustomRecord>, IQueryable<CustomRecord>>> EntityOrderingCache = new(EntityOrderComparer.Instance);

    private static readonly ConcurrentDictionary<(MethodInfo Method, string Key), DynamicFieldSelectors> DynamicSelectorCache = new(DynamicSelectorComparer.Instance);

    private static readonly Expression<Func<CustomRecord, Guid>> IdSelector = r => r.Id;

    private static readonly MethodInfo JsonbExtractPathTextMethod = typeof(JsonbFunctionsExtensions)
        .GetMethod(nameof(JsonbFunctionsExtensions.JsonbExtractPathText), [typeof(JsonObject), typeof(string)])!;

    private static readonly MethodInfo JsonbExtractPathNumberMethod = typeof(JsonbFunctionsExtensions)
        .GetMethod(nameof(JsonbFunctionsExtensions.JsonbExtractPathNumber), [typeof(JsonObject), typeof(string)])!;

    private static readonly MethodInfo JsonbExtractPathBooleanMethod = typeof(JsonbFunctionsExtensions)
        .GetMethod(nameof(JsonbFunctionsExtensions.JsonbExtractPathBoolean), [typeof(JsonObject), typeof(string)])!;

    private static readonly MethodInfo JsonbExtractPathDateMethod = typeof(JsonbFunctionsExtensions)
        .GetMethod(nameof(JsonbFunctionsExtensions.JsonbExtractPathDate), [typeof(JsonObject), typeof(string)])!;

    private static readonly MethodInfo JsonbExtractPathDateTimeMethod = typeof(JsonbFunctionsExtensions)
        .GetMethod(nameof(JsonbFunctionsExtensions.JsonbExtractPathDateTime), [typeof(JsonObject), typeof(string)])!;

    /// <summary>
    /// Filters custom records by table ID and an optional full-text search term across the JSON payload.
    /// </summary>
    /// <param name="query">The input custom record queryable sequence.</param>
    /// <param name="customTableId">The unique identifier of the target custom table.</param>
    /// <param name="searchTerm">The optional search term to match against the PostgreSQL tsvector shadow property.</param>
    /// <returns>The filtered queryable sequence.</returns>
    public static IQueryable<CustomRecord> FilterByTableAndSearch(
        this IQueryable<CustomRecord> query,
        Guid customTableId,
        string? searchTerm)
    {
        return string.IsNullOrWhiteSpace(searchTerm)
            ? query.Where(r => r.CustomTableId == customTableId)
            : query.Where(r => r.CustomTableId == customTableId
                && EF.Property<NpgsqlTsVector>(r, CustomRecordConstants.SearchVectorPropertyName)
                    .Matches(EF.Functions.WebSearchToTsQuery(CustomRecordConstants.SearchVectorConfiguration, searchTerm)));
    }

    /// <summary>
    /// Applies dynamic sorting to the query based on entity properties or dynamic JSONB keys within <see cref="CustomRecord.Data"/>.
    /// Automatically converts JSON values to appropriate SQL data types (<c>numeric</c>, <c>boolean</c>, <c>date</c>, <c>timestamp</c>, or <c>text</c>).
    /// </summary>
    /// <param name="query">The input <see cref="IQueryable{CustomRecord}"/> sequence to be sorted.</param>
    /// <param name="orderBy">The property name or JSONB key path (e.g., "name", "data.age", "data['age']").</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
    /// <param name="fieldDataType">The optional target data type used to perform strongly-typed SQL casting for dynamic JSON values.</param>
    /// <returns>The sorted <see cref="IQueryable{CustomRecord}"/> sequence.</returns>
    public static IQueryable<CustomRecord> OrderByCustomRecord(
        this IQueryable<CustomRecord> query,
        string? orderBy,
        string? sortOrder,
        FieldDataType? fieldDataType = null)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return query.OrderBy(r => r.Id);
        }

        bool isDescending = string.Equals(sortOrder, SortOrderConstants.Descending, StringComparison.OrdinalIgnoreCase);

        if (CustomRecordExtensions.SortablePropertiesMap.TryGetValue(orderBy, out var canonicalProperty))
        {
            return ApplyEntityPropertyOrdering(query, canonicalProperty, isDescending);
        }

        var jsonKey = CustomFieldExtensions.ExtractKey(orderBy);

        return string.IsNullOrWhiteSpace(jsonKey)
            ? query.OrderBy(r => r.Id)
            : fieldDataType switch
            {
                FieldDataType.Number => ApplyTypedOrdering<decimal?>(query, JsonbExtractPathNumberMethod, jsonKey, isDescending),
                FieldDataType.Boolean => ApplyTypedOrdering<bool?>(query, JsonbExtractPathBooleanMethod, jsonKey, isDescending),
                FieldDataType.Date => ApplyTypedOrdering<DateOnly?>(query, JsonbExtractPathDateMethod, jsonKey, isDescending),
                FieldDataType.DateTime => ApplyTypedOrdering<DateTime?>(query, JsonbExtractPathDateTimeMethod, jsonKey, isDescending),
                FieldDataType.Text or
                FieldDataType.Email or
                FieldDataType.PhoneNumber or
                FieldDataType.Url or
                _ => ApplyTypedOrdering<string?>(query, JsonbExtractPathTextMethod, jsonKey, isDescending),
            };
    }

    private static IQueryable<CustomRecord> ApplyEntityPropertyOrdering(
        IQueryable<CustomRecord> query,
        string propertyName,
        bool isDescending)
    {
        var orderFunc = EntityOrderingCache.GetOrAdd((propertyName, isDescending), static key =>
        {
            var (prop, desc) = key;
            var param = Expression.Parameter(typeof(CustomRecord), "r");
            var propertyAccess = Expression.Property(param, prop);
            var propType = propertyAccess.Type;
            var lambda = Expression.Lambda(propertyAccess, param);
            return q =>
            {
                var orderMethod = desc ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
                var resultExpr = Expression.Call(
                    typeof(Queryable),
                    orderMethod,
                    [typeof(CustomRecord), propType],
                    q.Expression,
                    Expression.Quote(lambda));
                resultExpr = Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.ThenBy),
                    [typeof(CustomRecord), typeof(Guid)],
                    resultExpr,
                    Expression.Quote(IdSelector));
                return q.Provider.CreateQuery<CustomRecord>(resultExpr);
            };
        });
        return orderFunc(query);
    }

    private static IQueryable<CustomRecord> ApplyTypedOrdering<TProp>(
        IQueryable<CustomRecord> query,
        MethodInfo extractMethod,
        string jsonKey,
        bool isDescending)
    {
        var selectors = DynamicSelectorCache.GetOrAdd(
            (extractMethod, jsonKey),
            static key =>
            {
                var param = Expression.Parameter(typeof(CustomRecord), "r");
                var dataProperty = Expression.Property(param, nameof(CustomRecord.Data));
                var call = Expression.Call(key.Method, dataProperty, Expression.Constant(key.Key));
                var keySelector = Expression.Lambda<Func<CustomRecord, TProp>>(call, param);

                var isNullExpr = Expression.Lambda<Func<CustomRecord, bool>>(
                    Expression.Equal(call, Expression.Constant(null, typeof(TProp))),
                    param);
                return new DynamicFieldSelectors(keySelector, isNullExpr);
            });
        var typedKeySelector = (Expression<Func<CustomRecord, TProp>>)selectors.KeySelector;

        return !isDescending
            ? query.OrderBy(typedKeySelector).ThenBy(IdSelector)
            : query.OrderBy(selectors.IsNullSelector)
                    .ThenByDescending(typedKeySelector)
                    .ThenBy(IdSelector);
    }

    private readonly record struct DynamicFieldSelectors(LambdaExpression KeySelector, Expression<Func<CustomRecord, bool>> IsNullSelector);

    private sealed class EntityOrderComparer : IEqualityComparer<(string PropertyName, bool IsDescending)>
    {
        public static readonly EntityOrderComparer Instance = new();

        public bool Equals((string PropertyName, bool IsDescending) x, (string PropertyName, bool IsDescending) y)
            => x.IsDescending == y.IsDescending && string.Equals(x.PropertyName, y.PropertyName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string PropertyName, bool IsDescending) obj)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PropertyName), obj.IsDescending);
    }

    private sealed class DynamicSelectorComparer : IEqualityComparer<(MethodInfo Method, string Key)>
    {
        public static readonly DynamicSelectorComparer Instance = new();

        public bool Equals((MethodInfo Method, string Key) x, (MethodInfo Method, string Key) y)
            => ReferenceEquals(x.Method, y.Method) && string.Equals(x.Key, y.Key, StringComparison.Ordinal);

        public int GetHashCode((MethodInfo Method, string Key) obj)
            => HashCode.Combine(obj.Method, StringComparer.Ordinal.GetHashCode(obj.Key));
    }
}
