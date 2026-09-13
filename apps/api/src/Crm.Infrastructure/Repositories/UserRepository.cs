using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

public class UserRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<User>(appDbContext),
    IUserRepository
{
    /// <summary>
    /// Asynchronously checks if there is at least one user in the system.
    /// </summary>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>True if at least one user exists; otherwise, false.</returns>
    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().AnyAsync(ct);
    }

    /// <summary>
    /// Asynchronously checks whether a user with the specified identifier is active in the system.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to check.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains <c>true</c> if the user exists and is active; otherwise, <c>false</c>.
    /// </returns>
    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default)
    {
        bool isActive = await DbSet
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(x => x.IsActive)
            .FirstOrDefaultAsync(ct);

        return isActive;
    }
}
