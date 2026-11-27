using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository operations for managing <see cref="TableTemplate"/> entities in the database.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class TableTemplateRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<TableTemplate>(appDbContext),
    ITableTemplateRepository
{
    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        bool? onlySystemPresets = null,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => GetPagedAsync<TResult>(
            predicate: BuildTableTemplateFilter(searchTerm, onlySystemPresets),
            orderBy: orderBy,
            sortOrder: sortOrder,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<List<TableTemplate>> GetSystemPresetsForUpdateAsync(CancellationToken cancellationToken = default)
        => DbSet
            .Where(t => t.IsSystemPreset)
            .ToListAsync(cancellationToken);

    private static Expression<Func<TableTemplate, bool>>? BuildTableTemplateFilter(string? searchTerm, bool? onlySystemPresets)
    {
        Expression<Func<TableTemplate, bool>>? presetFilter = onlySystemPresets.HasValue
            ? t => t.IsSystemPreset == onlySystemPresets.Value
            : null;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return presetFilter;
        }

        Expression<Func<TableTemplate, bool>> searchFilter = QueryFilterExpressions.ContainsIgnoreCase<TableTemplate>(
            searchTerm,
            t => t.Name,
            t => t.Description);

        return presetFilter is null
            ? searchFilter
            : presetFilter.And(searchFilter).Expand();
    }
}
