using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

public interface IAuthRoleRepository :
    IProjectingRepository,
    IExistenceChecker
{
    /// <summary>
    /// Asynchronously checks if an entity with the specified role name exists.
    /// Throws an exception if the entity is not found.
    /// </summary>
    /// <param name="roleName">The name of the role to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if no entity with the specified role name exists.</exception>
    Task EnsureExistsAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the unique identifier of an application role by its name.
    /// </summary>
    /// <param name="roleName">The name of the role to find.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the role's <see cref="Guid"/> identifier.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if no role with the specified name exists.</exception>
    Task<Guid> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all roles assigned to a specific user by their unique identifier,
    /// projected to the target type <typeparamref name="TRole"/> using compile-time projection.
    /// </summary>
    /// <typeparam name="TRole">The type of the role DTO/model to project into via Mapperly.</typeparam>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of projected roles assigned to the user.</returns>
    Task<List<TRole>> GetRolesByUserAsync<TRole>(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the unique identifiers of all roles assigned to a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of role IDs assigned to the user.</returns>
    Task<List<Guid>> GetRoleIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected role entities based on the search term.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="searchTerm">Optional search term to filter roles by name, feminitive name, or plural name.</param>
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
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if the specified user is the last active administrator in the system.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the user is the last active administrator; otherwise, false.</returns>
    Task<bool> IsUserLastAdminAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the names of the roles matching the specified identifiers.
    /// Throws an exception containing all missing role IDs if any role is not found.
    /// </summary>
    /// <param name="roleIds">The collection of role identifiers to fetch names for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of role names corresponding to the provided identifiers.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if one or more role IDs do not exist.</exception>
    Task<List<string>> GetRoleNamesByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all role permissions from the database, grouped by their respective role identifiers.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <see cref="Dictionary{TKey, TValue}"/>
    /// where the key is the Role ID (<see cref="Guid"/>) and the value is a <see cref="HashSet{T}"/> of permission strings (claims)
    /// associated with that role.
    /// </returns>
    Task<Dictionary<Guid, HashSet<string>>> GetAllRolesPermissionsAsync(CancellationToken cancellationToken = default);
}
