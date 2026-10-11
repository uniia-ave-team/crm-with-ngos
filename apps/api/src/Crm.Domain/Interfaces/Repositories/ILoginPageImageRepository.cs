using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines the contract for the repository managing <see cref="LoginPageImage"/> entities.
/// Inherits standard data access operations from <see cref="IGenericRepository{T}"/>.
/// </summary>
public interface ILoginPageImageRepository : IGenericRepository<LoginPageImage>
{
    /// <summary>
    /// Asynchronously retrieves only the file name (or URL) of a specific login page image by its unique identifier.
    /// Utilizes projection to optimize database querying by selecting only the required field.
    /// </summary>
    /// <param name="id">The unique identifier of the login page image.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file name or URL.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the login page image is not found in the database.</exception>
    /// <exception cref="EntityFieldNotFoundException">Thrown if the login page image exists but its file reference is not set.</exception>
    Task<string> GetUrlAsync(Guid id, CancellationToken cancellationToken = default);

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
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
    string? searchTerm = null,
    string? orderBy = null,
    string? sortOrder = SortOrderConstants.Ascending,
    int pageNumber = PaginationConstants.MinPageNumber,
    int pageSize = PaginationConstants.DefaultPageSize,
    CancellationToken cancellationToken = default);
}
