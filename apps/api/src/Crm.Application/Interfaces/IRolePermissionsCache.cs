namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a high-performance caching abstraction for role-based access rights.
/// Designed with asynchronous signatures to facilitate a seamless future migration
/// to distributed caching systems (e.g., Redis) without impacting the Application layer.
/// </summary>
public interface IRolePermissionsCache
{
    /// <summary>
    /// Retrieves the unique set of access right claims assigned to the specified role.
    /// If the access rights are missing from the cache, the implementation must fetch them
    /// from the underlying data store and cache the result.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, containing a <see cref="HashSet{String}"/> of access right values.</returns>
    Task<HashSet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites the cached access rights for a specific role with a provided set.
    /// Should be called immediately after any role claims are modified in the database.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="rights">The comprehensive set of access right values to cache.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cache update operation.</returns>
    Task SetRolePermissionsAsync(Guid roleId, HashSet<string> rights, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the access rights associated with a specific role from the cache.
    /// </summary>
    /// <param name="roleId">The identifier of the role to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task RemoveRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Warms up the cache by actively fetching all roles and their associated access rights
    /// from the database. Intended to be executed during application startup to achieve zero cache misses.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cache seeding operation.</returns>
    Task SeedAllRolesCacheAsync(CancellationToken cancellationToken = default);
}
