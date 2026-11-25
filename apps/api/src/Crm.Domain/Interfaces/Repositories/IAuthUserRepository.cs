using Crm.Domain.Common;
using Crm.Domain.Constants;
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
    /// Asynchronously retrieves and projects an entity by its email address.
    /// </summary>
    /// <typeparam name="TResult">The target projection type.</typeparam>
    /// <param name="email">The email address of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected result associated with the specified email address.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified email address is not found.</exception>
    Task<TResult> GetAsync<TResult>(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected entities based on the search term
    /// and standard or custom field sorting.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="searchTerm">Optional search term to filter entities (by core profile fields and visible custom field values).</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (e.g., <see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index). Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <param name="showDeleted">A value indicating whether to include soft-deleted entities in the results. Defaults to false.</param>
    /// <param name="customFieldSort">Optional resolved custom field sort specification for typed SQL ordering.</param>
    /// <param name="searchablePublicCustomFieldKeys">Optional list of public custom field JSONB keys eligible for text search across all users.</param>
    /// <param name="searchablePrivateCustomFieldKeys">Optional list of private custom field JSONB keys eligible for text search on the requesting user's own row.</param>
    /// <param name="currentUserId">The optional identifier of the requesting user for self-visibility search enforcement.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paginated collection of projected items along with pagination metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        bool showDeleted = false,
        UserCustomFieldSort? customFieldSort = null,
        IReadOnlyList<string>? searchablePublicCustomFieldKeys = null,
        IReadOnlyList<string>? searchablePrivateCustomFieldKeys = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that an entity with the specified email address does not already exist.
    /// Throws an exception if an entity with this email is found.
    /// </summary>
    /// <param name="email">The email address to check for uniqueness.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    /// <exception cref="EntityAlreadyExistsException">Thrown if an entity with the specified email already exists.</exception>
    Task EnsureDoesNotExistAsync(string email, CancellationToken cancellationToken = default);
}
