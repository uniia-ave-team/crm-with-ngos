using Crm.Domain.Common;
using Crm.Domain.Constants;
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
    /// Asynchronously retrieves a single random login page image and projects it to <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The target projection type.</typeparam>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A random projected login page image.</returns>
    /// <exception cref="NotFoundException">Thrown if no login page images exist in the data store.</exception>
    Task<TResult> GetRandomAsync<TResult>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves only the file name (or URL) of a specific login page image by its unique identifier.
    /// Utilizes projection to optimize database querying by selecting only the required field.
    /// </summary>
    /// <param name="id">The unique identifier of the login page image.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file name or URL.</returns>
    /// <exception cref="EntityFieldNotFoundException{LoginPageImage}">Thrown if the login page image is not found in the database.</exception>
    Task<string> GetUrlAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected login page image entities based on the search term.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements.</typeparam>
    /// <param name="searchTerm">Optional search term to filter images by URL.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
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
