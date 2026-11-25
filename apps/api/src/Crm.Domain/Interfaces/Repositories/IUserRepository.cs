using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="User"/> entities.
/// </summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Asynchronously retrieves only the avatar file name of a specific user.
    /// Utilizes projection to optimize database querying by selecting only the required field.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the avatar file name.</returns>
    /// <exception cref="NotFoundException">Thrown if the user is not found in the database or if the user exists but their avatar is not set.</exception>
    Task<string> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if there is at least one user in the system.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if at least one user exists; otherwise, false.</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks whether a user with the specified identifier is active in the system.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains <c>true</c> if the user is active, or <c>false</c> if the user is deactivated.
    /// </returns>
    /// <exception cref="EntityNotFoundException{User}">Thrown if no user with the specified identifier exists.</exception>
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously assigns all unassigned users (where NgoId is null or empty) to the specified NGO using a bulk update operation.
    /// </summary>
    /// <param name="ngoId">The unique identifier of the NGO to assign the users to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing the number of rows updated in the database.</returns>
    Task<int> AssignUnassignedUsersToNgoAsync(Guid ngoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates the activation status of a specific user using a direct database update operation.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="isActive">The new activation status to apply (e.g., false to deactivate).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing the number of rows updated in the database.</returns>
    Task<int> SetUserActivationStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves activation flags (<see cref="User.IsActive"/>) for the specified user identifiers without loading full entities.
    /// </summary>
    /// <param name="ids">The collection of user identifiers to look up.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary mapping each existing user identifier to its activation status.</returns>
    Task<Dictionary<Guid, bool>> GetActiveStatusesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates the <see cref="User.LastLoginAt"/> UTC timestamp for the specified user using a direct database update.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="lastLoginAt">The UTC timestamp of the successful authentication.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateLastLoginAtAsync(Guid userId, DateTime lastLoginAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines which of the specified custom field keys currently have at least one non-null value
    /// stored inside any user's <see cref="User.CustomFields"/> JSONB payload.
    /// </summary>
    /// <param name="keys">The candidate schema keys to inspect.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A set of keys that have at least one non-null value stored on any user.</returns>
    Task<HashSet<string>> GetKeysWithNonNullCustomFieldValuesAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all users' <see cref="User.CustomFields"/> JSONB documents in a single bulk SQL update
    /// according to <paramref name="diff"/> (dropping removed keys, renaming keys, and backfilling newly required defaults).
    /// </summary>
    /// <param name="diff">The computed schema diff.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of user rows updated in the database.</returns>
    Task<int> SyncCustomFieldsSchemaAsync(
        SchemaDiffResult diff,
        CancellationToken cancellationToken = default);
}
