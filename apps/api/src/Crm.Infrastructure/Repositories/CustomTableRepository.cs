using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository operations for managing <see cref="CustomTable"/> entities and querying their <see cref="CustomField"/> schemas.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class CustomTableRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<CustomTable>(appDbContext),
    ICustomTableRepository
{
    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid ngoId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => GetPagedAsync<TResult>(
            predicate: BuildCustomTableFilter(searchTerm, ngoId),
            orderBy: orderBy,
            sortOrder: sortOrder,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async Task<CustomTable> GetForUpdateWithFieldsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException<CustomTable>(id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomField>> GetFieldsByCustomTableIdAsync(
        Guid customTableId,
        CancellationToken cancellationToken = default)
        => await Context.Set<CustomField>()
            .AsNoTracking()
            .Where(x => x.CustomTableId == customTableId)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task ApplyFieldSchemaChangesAsync(
        Guid customTableId,
        IEnumerable<CustomField> addedFields,
        IEnumerable<CustomField> removedFields,
        CancellationToken cancellationToken = default)
    {
        var added = addedFields as IReadOnlyCollection<CustomField> ?? [.. addedFields];
        foreach (var field in added)
        {
            field.CustomTableId = customTableId;
            Context.Entry(field).State = EntityState.Added;
        }

        var removed = removedFields as IReadOnlyCollection<CustomField> ?? [.. removedFields];
        if (removed.Count > 0)
        {
            Context.Set<CustomField>().RemoveRange(removed);
        }

        return Task.CompletedTask;
    }

    private static Expression<Func<CustomTable, bool>> BuildCustomTableFilter(string? searchTerm, Guid ngoId)
    {
        Expression<Func<CustomTable, bool>> ngoFilter = t => t.NgoId == ngoId;

        return string.IsNullOrWhiteSpace(searchTerm)
            ? ngoFilter
            : ngoFilter.And(
                QueryFilterExpressions.ContainsIgnoreCase<CustomTable>(searchTerm, t => t.Name, t => t.Description))
                .Expand();
    }
}
