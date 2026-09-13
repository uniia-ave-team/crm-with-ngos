using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

public class NgoRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<Ngo>(appDbContext),
    INgoRepository
{
    /// <summary>
    /// Asynchronously checks if an NGO already exists in the database.
    /// Throws an exception if an NGO is found.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityAlreadyExistsException">Thrown if an NGO already exists in the database.</exception>
    public async Task EnsureDoesNotExistAsync(CancellationToken cancellationToken = default)
    {
        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new EntityAlreadyExistsException(nameof(Ngo));
        }
    }

    /// <summary>
    /// Asynchronously retrieves the single NGO entity.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved NGO entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    public async Task<Ngo> GetAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException(nameof(Ngo));

    /// <summary>
    /// Asynchronously checks if there is NGO in the system.
    /// </summary>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>True if at least one NGO exists; otherwise, false.</returns>
    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().AnyAsync(ct);
    }
}
