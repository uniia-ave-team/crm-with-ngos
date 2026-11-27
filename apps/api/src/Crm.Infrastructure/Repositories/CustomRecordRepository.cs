using System.Text.Json;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Sorting;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository operations for managing <see cref="CustomRecord"/> entities in the database,
/// including native B-tree fractional-index ordering, smart column sorting, and atomic JSONB schema sync.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class CustomRecordRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<CustomRecord>(appDbContext),
    ICustomRecordRepository
{
    private const string TemporaryRebalancePrefix = "~";
    private const int RebalanceBatchSize = 5_000;

    /// <inheritdoc />
    public Task<bool> AnyAsync(Guid customTableId, CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .AnyAsync(r => r.CustomTableId == customTableId, cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid customTableId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CustomRecord> filteredQuery = DbSet
            .AsNoTracking()
            .FilterByTableAndSearch(customTableId, searchTerm);

        IQueryable<CustomRecord> orderedQuery = await ApplyOrderingAsync(
            customTableId,
            filteredQuery,
            orderBy,
            sortOrder,
            cancellationToken);

        return await ToPagedResultAsync<TResult>(orderedQuery, pageNumber, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CustomRecordPositionInfo?> GetPositionInfoAsync(Guid id, CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new CustomRecordPositionInfo(r.Id, r.CustomTableId, r.Position))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, CustomRecordPositionInfo>> GetPositionInfosByIdsAsync(
        Guid customTableId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => ids.Count == 0
            ? []
            : await DbSet
                .AsNoTracking()
                .Where(r => r.CustomTableId == customTableId && ids.Contains(r.Id))
                .Select(r => new CustomRecordPositionInfo(r.Id, r.CustomTableId, r.Position))
                .ToDictionaryAsync(r => r.Id, cancellationToken);

    /// <inheritdoc />
    public Task<string?> GetLastPositionAsync(Guid customTableId, CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .Where(r => r.CustomTableId == customTableId)
            .OrderByDescending(r => r.Position)
            .Select(r => r.Position)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> HasRecordInPositionRangeAsync(
        Guid customTableId,
        string? exclusiveLowerBound = null,
        string? exclusiveUpperBound = null,
        Guid? excludeRecordId = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CustomRecord> query = DbSet
            .AsNoTracking()
            .Where(r => r.CustomTableId == customTableId);

        if (excludeRecordId is { } excludedId)
        {
            query = query.Where(r => r.Id != excludedId);
        }

        if (exclusiveLowerBound is not null)
        {
            query = query.Where(r => string.Compare(r.Position, exclusiveLowerBound) > 0);
        }

        if (exclusiveUpperBound is not null)
        {
            query = query.Where(r => string.Compare(r.Position, exclusiveUpperBound) < 0);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdatePositionAsync(Guid id, string position, CancellationToken cancellationToken = default)
        => DbSet
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Position, position), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetOrderedIdsAsync(Guid customTableId, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(r => r.CustomTableId == customTableId)
            .OrderBy(r => r.Position)
            .ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ReplaceAllPositionsAsync(
        Guid customTableId,
        IReadOnlyDictionary<Guid, string> positionsById,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positionsById);

        if (positionsById.Count == 0)
        {
            return;
        }

        // Shift all table rows temporarily out of the base-62 keyspace using a fixed 37-char unique key ('~' + UUID)
        // so compact ranks can be written without unique-index collisions or risking varchar(256) overflow.
        await DbSet
            .Where(r => r.CustomTableId == customTableId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Position, r => TemporaryRebalancePrefix + r.Id.ToString()),
                cancellationToken);

        // Apply compact ranks in set-based batches via PostgreSQL's built-in jsonb_extract_path_text (O(log N) lookup),
        // requiring only 1 UPDATE statement for tables up to 5,000 rows with zero raw SQL and zero audit/timestamp churn.
        foreach (var chunk in positionsById.Chunk(RebalanceBatchSize))
        {
            var batchIds = new Guid[chunk.Length];
            var chunkMap = new Dictionary<string, string>(chunk.Length, StringComparer.Ordinal);

            for (int i = 0; i < chunk.Length; i++)
            {
                batchIds[i] = chunk[i].Key;
                chunkMap[chunk[i].Key.ToString()] = chunk[i].Value;
            }

            using var rankMap = JsonSerializer.SerializeToDocument(chunkMap);

            await DbSet
                .Where(r => r.CustomTableId == customTableId && batchIds.Contains(r.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(
                        r => r.Position,
                        r => JsonbFunctionsExtensions.JsonbExtractPathText(rankMap, r.Id.ToString()) ?? r.Position),
                    cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<int> SyncRecordSchemaAsync(Guid customTableId, SchemaSyncPlan plan, CancellationToken cancellationToken = default)
        => !plan.RequiresJsonbSync
            ? Task.FromResult(0)
            : DbSet
                .Where(r => r.CustomTableId == customTableId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(
                        r => r.Data,
                        r => JsonbFunctionsExtensions.SyncJsonbKeys(r.Data, plan.KeysToDrop, plan.KeysToBackfill)),
                    cancellationToken);

    private async Task<IQueryable<CustomRecord>> ApplyOrderingAsync(
        Guid customTableId,
        IQueryable<CustomRecord> filteredQuery,
        string? orderBy,
        string? sortOrder,
        CancellationToken cancellationToken)
    {
        bool isDescending = sortOrder.IsDescending();

        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return isDescending
                ? filteredQuery.OrderByDescending(r => r.Position).ThenBy(r => r.Id)
                : filteredQuery.OrderBy(r => r.Position).ThenBy(r => r.Id);
        }

        string trimmedOrderBy = orderBy.Trim();

        if (CustomRecordExtensions.SortablePropertiesMap.TryGetValue(trimmedOrderBy, out var canonicalProperty)
            && TypedPropertyOrderBuilder.TryApplyOrdering(filteredQuery, canonicalProperty, isDescending, out var scalarOrdered))
        {
            return scalarOrdered;
        }

        if (!CustomFieldExtensions.TryNormalizeFieldId(trimmedOrderBy, out string normalizedFieldId))
        {
            return filteredQuery.OrderBy(r => r.Id);
        }

        // Sample deterministically across the entire table in manual position order so inferred sort kind
        // stays stable across pages and independent of search filters.
        IOrderedQueryable<CustomRecord> tableOrderedQuery = DbSet
            .AsNoTracking()
            .Where(r => r.CustomTableId == customTableId)
            .OrderBy(r => r.Position)
            .ThenBy(r => r.Id);

        InferredSortKind sortKind = await tableOrderedQuery.DetectSortKindAsync(
            r => JsonbFunctionsExtensions.JsonbExtractPathText(r.Data, normalizedFieldId),
            cancellationToken);

        return filteredQuery.OrderByNormalizedFieldId(normalizedFieldId, sortKind, isDescending);
    }
}
