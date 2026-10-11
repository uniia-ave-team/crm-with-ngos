using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Asynchronously retrieves only the avatar file name of a specific user.
    /// Utilizes projection to optimize database querying by selecting only the required field.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the avatar file name.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the user is not found in the database.</exception>
    /// <exception cref="EntityFieldNotFoundException">Thrown if the user exists but their avatar is not set.</exception>
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
    /// The task result contains <c>true</c> if the user exists and is active; otherwise, <c>false</c>.
    /// </returns>
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
}
