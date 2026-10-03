using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Provides data access operations and specific queries for managing AuthUser entities.
/// </summary>
public interface IAuthUserRepository :
    IProjectingRepository,
    IExistenceChecker
{
    /// <summary>
    /// Asynchronously gets the total count of users assigned to a specific role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of users that have the specified role assigned.</returns>
    Task<int> GetCountByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves and projects an entity by its email address using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="email">The email address of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected entity DTO associated with the specified email address.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified email address is not found.</exception>
    Task<TResult> GetAsync<TResult>(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected entities based on the search term.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="searchTerm">Optional search term to filter entities (e.g., by name, email, or other relevant fields).</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (e.g., "asc" or "desc"). Defaults to ascending.</param>
    /// <param name="pageNumber">The current page number (1-based index). Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <param name="showDeleted">A value indicating whether to include soft-deleted entities in the results. Defaults to false.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paginated collection of projected items along with pagination metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        bool showDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that an entity with the specified email address does not already exist.
    /// Throws an exception if an entity with this email is found.
    /// </summary>
    /// <param name="email">The email address to check for uniqueness.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    /// <exception cref="EntityAlreadyExistsException">Thrown if an entity with the specified email already exists.</exception>
    Task EnsureNotExistsAsync(string email, CancellationToken cancellationToken = default);
}
