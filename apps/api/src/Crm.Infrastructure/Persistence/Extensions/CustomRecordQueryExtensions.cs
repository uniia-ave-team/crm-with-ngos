using System.Linq.Expressions;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
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
    /// Applies dynamic sorting to the query based on scalar entity properties or canonical JSONB field ID keys within <see cref="CustomRecord.Data"/>.
    /// </summary>
    /// <param name="query">The input <see cref="IQueryable{CustomRecord}"/> sequence to be sorted.</param>
    /// <param name="orderBy">The scalar property name or custom field GUID string.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
    /// <param name="sortKind">The inferred smart sort kind used when sorting by a JSONB key.</param>
    /// <returns>The sorted <see cref="IQueryable{CustomRecord}"/> sequence.</returns>
    public static IQueryable<CustomRecord> OrderByCustomRecord(
        this IQueryable<CustomRecord> query,
        string? orderBy,
        string? sortOrder,
        InferredSortKind sortKind = InferredSortKind.Text)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return query.OrderBy(IdSelector);
        }

        bool isDescending = sortOrder.IsDescending();
        string trimmed = orderBy.Trim();

        return CustomRecordExtensions.SortablePropertiesMap.TryGetValue(trimmed, out var canonicalProperty)
            && TypedPropertyOrderBuilder.TryApplyOrdering(query, canonicalProperty, isDescending, out var ordered)
                ? ordered
                : CustomFieldExtensions.TryNormalizeFieldId(trimmed, out string normalizedFieldId)
                    ? query.OrderByNormalizedFieldId(normalizedFieldId, sortKind, isDescending)
                    : query.OrderBy(IdSelector);
    }

    /// <summary>
    /// Applies smart NULLS-LAST ordering against a pre-normalized JSONB field ID key within <see cref="CustomRecord.Data"/>.
    /// </summary>
    /// <param name="query">The input <see cref="IQueryable{CustomRecord}"/> sequence to be sorted.</param>
    /// <param name="normalizedFieldId">The canonical lowercase GUID string matching the JSONB property key.</param>
    /// <param name="sortKind">The inferred smart sort kind for the column.</param>
    /// <param name="isDescending">Whether to sort non-null values in descending order.</param>
    /// <returns>The sorted <see cref="IQueryable{CustomRecord}"/> sequence.</returns>
    public static IQueryable<CustomRecord> OrderByNormalizedFieldId(
        this IQueryable<CustomRecord> query,
        string normalizedFieldId,
        InferredSortKind sortKind,
        bool isDescending)
        => query.ApplySmartNullsLastOrdering(
            r => JsonbFunctionsExtensions.JsonbExtractPathText(r.Data, normalizedFieldId),
            sortKind,
            IdSelector,
            isDescending);
}
