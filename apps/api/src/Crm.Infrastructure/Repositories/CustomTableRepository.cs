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

/// <summary>
/// Provides repository operations for managing <see cref="CustomTable"/> entities in the database.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class CustomTableRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<CustomTable>(appDbContext),
    ICustomTableRepository
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected custom tables.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="ngoId">Filter to retrieve custom tables belonging to a specific NGO.</param>
    /// <param name="searchTerm">Optional search term to filter custom tables by Name or Description.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected custom table items.</returns>
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

    /// <summary>
    /// Asynchronously retrieves a custom table by its unique identifier along with its fields for updating, with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="CustomTable"/> entity including its related fields.</returns>
    /// <exception cref="NotFoundException">Thrown if the custom table with the specified ID is not found.</exception>
    public async Task<CustomTable> GetForUpdateWithFieldsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException<CustomTable>(id);
    }

    /// <summary>
    /// Builds the filter expression for querying custom tables based on search term and NGO ID.
    /// </summary>
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF Core requires ToLower for correct SQL translation")]
    private static Expression<Func<CustomTable, bool>> BuildCustomTableFilter(string? searchTerm, Guid ngoId)
    {
        bool hasSearch = !string.IsNullOrWhiteSpace(searchTerm);
        string term = hasSearch ? searchTerm.Trim().ToLowerInvariant() : string.Empty;

        return t =>
            t.NgoId == ngoId &&
            (!hasSearch || t.Name.ToLower().Contains(term) || (t.Description != null && t.Description.ToLower().Contains(term)));
    }
}
