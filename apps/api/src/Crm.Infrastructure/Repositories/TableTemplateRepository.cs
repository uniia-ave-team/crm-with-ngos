using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Crm.Infrastructure.Persistence.Extensions;
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
    public async Task<TableTemplate> GetForUpdateWithFieldsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException<TableTemplate>(id);
    }

    /// <inheritdoc />
    public Task<List<string>> GetSystemPresetNamesAsync(CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .Where(t => t.IsSystemPreset)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task ResolveFieldKeyCollisionsAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
        => Context.ResolveFieldKeyCollisionsAsync<TemplateField>(templateId, f => f.TemplateId, cancellationToken);

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
            : ProjectVisibilityExpressions.CombineAnd(presetFilter, searchFilter);
    }
}
