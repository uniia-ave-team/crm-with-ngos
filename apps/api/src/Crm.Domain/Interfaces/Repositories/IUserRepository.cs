using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing <see cref="User"/> entities.
/// </summary>
public interface IUserRepository : IGenericRepository<User>, IRowLockRepository
{
    /// <summary>
    /// Retrieves the avatar file path for a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The avatar file path or URL string.</returns>
    Task<string> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks asynchronously whether any users currently exist in the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if at least one user exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks asynchronously whether the specified user is currently active.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the user is active; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns all users who currently do not belong to an NGO to the specified organization.
    /// </summary>
    /// <param name="ngoId">The unique identifier of the NGO to assign the users to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of user records updated.</returns>
    Task<int> AssignUnassignedUsersToNgoAsync(Guid ngoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically updates the activation status of the specified user directly in the database.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="isActive">The target activation status.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of affected database rows.</returns>
    Task<int> SetUserActivationStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active status flags for a batch of user identifiers without tracking.
    /// </summary>
    /// <param name="ids">The collection of user identifiers to query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary mapping existing user identifiers to their <see cref="User.IsActive"/> status.</returns>
    Task<Dictionary<Guid, bool>> GetActiveStatusesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the <see cref="User.LastLoginAt"/> timestamp directly in the database without loading the entity.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="lastLoginAt">The UTC timestamp of the login event.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateLastLoginAtAsync(Guid userId, DateTime lastLoginAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all users' <see cref="User.CustomFields"/> JSONB payloads directly in the database
    /// by dropping removed keys and backfilling newly required keys in a single atomic statement.
    /// </summary>
    /// <param name="plan">The computed schema synchronization plan.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of user rows updated in the database.</returns>
    Task<int> SyncCustomFieldsSchemaAsync(SchemaSyncPlan plan, CancellationToken cancellationToken = default);
}
