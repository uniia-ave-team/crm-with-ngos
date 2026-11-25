using System.Linq.Expressions;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Sorting;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides extension methods for querying, filtering, and dynamically ordering <see cref="CustomRecord"/> sequences.
/// </summary>
public static class CustomRecordQueryExtensions
{
    private static readonly Expression<Func<CustomRecord, Guid>> IdSelector = r => r.Id;

    /// <summary>
    /// Filters custom records by table ID and an optional prefix full-text search term across the JSON payload values.
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
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return query.Where(r => r.CustomTableId == customTableId);
        }

        var prefixQuery = searchTerm.BuildPrefixTsQuery();
        return prefixQuery is null
            ? query.Where(_ => false)
            : query.Where(r => r.CustomTableId == customTableId
                && EF.Property<NpgsqlTsVector>(r, CustomRecordSearchConstants.SearchVectorPropertyName)
                    .Matches(EF.Functions.ToTsQuery(CustomRecordSearchConstants.SearchVectorConfiguration, prefixQuery)));
    }

    /// <summary>
    /// Applies dynamic sorting to the query based on entity properties or dynamic JSONB keys within <see cref="CustomRecord.Data"/>.
    /// Reuses <see cref="TypedPropertyOrderBuilder"/> for scalar properties and <see cref="TypedOrderingExtensions"/>
    /// for safe PostgreSQL typed casting (<c>numeric</c>, <c>boolean</c>, <c>date</c>, <c>timestamp</c>, or <c>text</c>).
    /// </summary>
    /// <param name="query">The input <see cref="IQueryable{CustomRecord}"/> sequence to be sorted.</param>
    /// <param name="orderBy">The property name or JSONB key path (e.g., <c>"name"</c>, <c>"data.age"</c>, <c>"data['age']"</c>).</param>
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
            return query.OrderBy(IdSelector);
        }

        bool isDescending = sortOrder.IsDescending();

        if (CustomRecordExtensions.SortablePropertiesMap.TryGetValue(orderBy.Trim(), out var canonicalProperty)
            && TypedPropertyOrderBuilder.TryApplyOrdering(query, canonicalProperty, isDescending, out var ordered))
        {
            return ordered;
        }

        var jsonKey = CustomFieldExtensions.ExtractKey(orderBy);

        return string.IsNullOrWhiteSpace(jsonKey)
            ? query.OrderBy(IdSelector)
            : query.ApplyTypedNullsLastOrdering(
                r => JsonbFunctionsExtensions.JsonbExtractPathText(r.Data, jsonKey),
                fieldDataType,
                IdSelector,
                isDescending);
    }
}
