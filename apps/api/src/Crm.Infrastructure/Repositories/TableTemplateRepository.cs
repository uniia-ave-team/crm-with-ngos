using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

public class TableTemplateRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<TableTemplate>(appDbContext),
    ITableTemplateRepository
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected table templates.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="onlySystemPresets">Optional filter to retrieve only system presets (true), user presets (false), or all (null).</param>
    /// <param name="searchTerm">Optional search term to filter templates by Name or Description.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected template items.</returns>
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

    /// <summary>
    /// Asynchronously retrieves a custom table template by its unique identifier along with its fields for updating, with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the table template to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="TableTemplate"/> entity including its related <see cref="TableTemplate.Fields"/>.</returns>
    /// <exception cref="NotFoundException">Thrown if the table template with the specified ID is not found.</exception>
    public async Task<TableTemplate> GetForUpdateWithFieldsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException<TableTemplate>(id);
    }

    /// <summary>
    /// Gets a list of existing system preset names to avoid duplicate seed inserts.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of system preset names.</returns>
    public async Task<List<string>> GetSystemPresetNamesAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.IsSystemPreset)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Builds the filter expression for querying table templates based on search term and system preset flag.
    /// </summary>
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF Core requires ToLower for correct SQL translation")]
    private static Expression<Func<TableTemplate, bool>> BuildTableTemplateFilter(string? searchTerm, bool? onlySystemPresets)
    {
        bool hasSearch = !string.IsNullOrWhiteSpace(searchTerm);
        string term = hasSearch ? searchTerm.Trim().ToLowerInvariant() : string.Empty;

        return t =>
            (!onlySystemPresets.HasValue || t.IsSystemPreset == onlySystemPresets.Value) &&
            (!hasSearch || t.Name.ToLower().Contains(term) || (t.Description != null && t.Description.ToLower().Contains(term)));
    }
}
