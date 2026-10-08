using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="User"/> entities.
/// </summary>
public class UserRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<User>(appDbContext),
    IUserRepository
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
    public async Task<string> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var projection = await DbSet
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.Avatar })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException(nameof(User));

        return string.IsNullOrWhiteSpace(projection.Avatar)
            ? throw new EntityFieldNotFoundException(nameof(User), nameof(User.Avatar))
            : projection.Avatar;
    }

    /// <summary>
    /// Asynchronously checks if there is at least one user in the system.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if at least one user exists; otherwise, false.</returns>
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously checks whether a user with the specified identifier is active in the system.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task whose result is <c>true</c> if the user is active, or <c>false</c> if deactivated.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the user profile with the specified ID is not found.</exception>
    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var isActive = await DbSet
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        return isActive ?? throw new EntityNotFoundException(nameof(User), userId);
    }

    /// <summary>
    /// Asynchronously assigns all unassigned users (where NgoId is null or empty) to the specified NGO using a bulk update operation.
    /// </summary>
    /// <param name="ngoId">The unique identifier of the NGO to assign the users to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing the number of rows updated in the database.</returns>
    public async Task<int> AssignUnassignedUsersToNgoAsync(Guid ngoId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(u => u.NgoId == null || u.NgoId == Guid.Empty)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.NgoId, ngoId), cancellationToken);
    }

    /// <summary>
    /// Asynchronously updates the activation status of a specific user using a direct database update operation.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="isActive">The new activation status to apply (e.g., false to deactivate).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing the number of rows updated in the database.</returns>
    public async Task<int> SetUserActivationStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive), cancellationToken);
    }
}
