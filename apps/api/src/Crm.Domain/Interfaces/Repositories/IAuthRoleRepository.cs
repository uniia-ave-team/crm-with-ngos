using Crm.Domain.Entities;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Interfaces.Repositories;

public interface IAuthRoleRepository : IGenericRepository<AuthRole>
{
    /// <summary>
    /// Asynchronously retrieves all roles assigned to a specific user by their unique identifier,
    /// projected to the target type <typeparamref name="TRole"/> using compile-time projection.
    /// </summary>
    /// <typeparam name="TRole">The type of the role DTO/model to project into via Mapperly.</typeparam>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A list of projected roles assigned to the user.</returns>
    Task<List<TRole>> GetRolesByUserAsync<TRole>(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously retrieves the unique identifiers of all roles assigned to a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A list of role IDs assigned to the user.</returns>
    Task<List<Guid>> GetRoleIdsByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously retrieves the names of the roles matching the specified identifiers.
    /// Throws an exception containing all missing role IDs if any role is not found.
    /// </summary>
    /// <param name="roleIds">The collection of role identifiers to fetch names for.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A list of role names corresponding to the provided identifiers.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if one or more role IDs do not exist.</exception>
    Task<List<string>> GetRoleNamesByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken ct = default);
}
