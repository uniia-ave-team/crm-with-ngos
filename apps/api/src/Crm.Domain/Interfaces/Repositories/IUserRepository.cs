using Crm.Domain.Entities;

namespace Crm.Domain.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Asynchronously checks if there is at least one user in the system.
    /// </summary>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>True if at least one user exists; otherwise, false.</returns>
    Task<bool> AnyAsync(CancellationToken ct = default);

    /// <summary>
    /// Asynchronously checks whether a user with the specified identifier is active in the system.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to check.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains <c>true</c> if the user exists and is active; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously assigns all unassigned users (where NgoId is null or empty) to the specified NGO using a bulk update operation.
    /// </summary>
    /// <param name="ngoId">The unique identifier of the NGO to assign the users to.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing the number of rows updated in the database.</returns>
    Task<int> AssignUnassignedUsersToNgoAsync(Guid ngoId, CancellationToken ct = default);
}
