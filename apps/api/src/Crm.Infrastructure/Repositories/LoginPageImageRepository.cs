using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Implements the repository for managing <see cref="LoginPageImage"/> entities.
/// Inherits standard data access operations from <see cref="GenericRepository{T}"/>.
/// </summary>
public class LoginPageImageRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<LoginPageImage>(appDbContext),
    ILoginPageImageRepository
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected login page image entities based on the search term.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="searchTerm">Optional search term to filter images by URL.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (e.g., "asc" or "desc"). Defaults to ascending.</param>
    /// <param name="pageNumber">The current page number (1-based index). Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paginated collection of projected items along with pagination metadata.</returns>
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => GetPagedAsync<TResult>(
            predicate: BuildImageFilter(searchTerm),
            orderBy: orderBy,
            sortOrder: sortOrder,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Builds the filter expression for querying login page images based on an optional search term.
    /// Matches against the image URL.
    /// </summary>
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF Core requires ToUpper for correct SQL translation")]
    private static Expression<Func<LoginPageImage, bool>>? BuildImageFilter(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        string term = searchTerm.Trim().ToUpperInvariant();

        return image => image.Url != null && image.Url.ToUpper().Contains(term);
    }
}
