using Crm.Application.Exceptions;
using Crm.Domain.Exceptions;

namespace Crm.Application.Interfaces;

/// <summary>
/// Defines the contract for managing roles, role assignments, and role permissions securely.
/// Keeps the application layer unaware of the underlying Identity framework.
/// </summary>
public interface IRoleIdentityService
{
    /// <summary>
    /// Asynchronously creates a new role with the specified name and optional linguistic variants.
    /// </summary>
    /// <param name="roleName">The name of the role to create.</param>
    /// <param name="feminitiveName">The feminine variant of the role name, if applicable.</param>
    /// <param name="pluralName">The plural variant of the role name, if applicable.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the newly created role.</returns>
    /// <exception cref="UserOperationException">Thrown if the role creation operation fails (e.g., role already exists).</exception>
    Task<Guid> CreateRoleAsync(string roleName, string? feminitiveName = null, string? pluralName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates an existing role with a new name and optional linguistic variants.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to update.</param>
    /// <param name="roleName">The new name for the role.</param>
    /// <param name="feminitiveName">The feminine variant of the role name, if applicable.</param>
    /// <param name="pluralName">The plural variant of the role name, if applicable.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the role with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the role update operation fails (e.g., role name already exists).</exception>
    Task UpdateRoleAsync(Guid roleId, string roleName, string? feminitiveName = null, string? pluralName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes an existing role by its unique identifier.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the role with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the role deletion operation fails.</exception>
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds a specific permission (claim) to a role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="permissionValue">The value of the permission to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the role with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the permission assignment operation fails.</exception>
    Task AddPermissionToRoleAsync(Guid roleId, string permissionValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a specific permission (claim) from a role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="permissionValue">The value of the permission to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the role with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the permission removal operation fails.</exception>
    Task RemovePermissionFromRoleAsync(Guid roleId, string permissionValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all permissions (claims) associated with a specific role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of permission values assigned to the role.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the role with the specified ID does not exist.</exception>
    Task<HashSet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);
}
