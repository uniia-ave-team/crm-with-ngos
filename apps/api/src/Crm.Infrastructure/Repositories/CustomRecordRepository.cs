using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository operations for managing <see cref="CustomRecord"/> entities in the database.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class CustomRecordRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<CustomRecord>(appDbContext),
    ICustomRecordRepository
{
    /// <inheritdoc />
    public Task<bool> AnyAsync(Guid customTableId, CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .AnyAsync(r => r.CustomTableId == customTableId, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid customTableId,
        string? searchTerm = null,
        string? orderBy = null,
        FieldDataType? fieldDataType = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .FilterByTableAndSearch(customTableId, searchTerm)
            .OrderByCustomRecord(orderBy, sortOrder, fieldDataType);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> SyncRecordSchemaAsync(Guid customTableId, SchemaDiffResult diff, CancellationToken cancellationToken = default)
    {
        var updateLambda = ((Expression<Func<CustomRecord, JsonObject>>)(r => r.Data)).ToSchemaUpdateLambda(diff);

        return updateLambda is null
            ? Task.FromResult(0)
            : DbSet
                .Where(r => r.CustomTableId == customTableId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Data, updateLambda), cancellationToken);
    }
}
