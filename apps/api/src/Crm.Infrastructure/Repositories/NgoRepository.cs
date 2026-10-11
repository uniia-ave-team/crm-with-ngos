using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Mapster;
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
    /// Asynchronously retrieves the first record from the data set and projects it to the target type.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected result (e.g., DTO).</typeparam>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the projected entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the record is not found in the database.</exception>
    public async Task<TResult> GetAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Ngo));
    }

    /// <summary>
    /// Asynchronously retrieves the single NGO entity with change tracking enabled.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved tracked NGO entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    public async Task<Ngo> GetForUpdateAsync(CancellationToken cancellationToken = default)
        => await DbSet.FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException(nameof(Ngo));

    /// <summary>
    /// Asynchronously retrieves the ID of the single NGO entity.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved NGO ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken = default)
        => await DbSet
            .Select(n => (Guid?)n.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Ngo));

    /// <summary>
    /// Asynchronously retrieves only the logo file name of the single NGO entity.
    /// Utilizes projection to optimize database querying by selecting only the required field.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the logo file name.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    /// <exception cref="EntityFieldNotFoundException">Thrown if the NGO exists but its logo is not set.</exception>
    public async Task<string> GetLogoAsync(CancellationToken cancellationToken = default)
    {
        var projection = await DbSet
            .AsNoTracking()
            .Select(x => new { x.Logo })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException(nameof(Ngo));

        return string.IsNullOrWhiteSpace(projection.Logo)
            ? throw new EntityFieldNotFoundException(nameof(Ngo), nameof(Ngo.Logo))
            : projection.Logo;
    }

    /// <summary>
    /// Asynchronously checks if there is NGO in the system.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if at least one NGO exists; otherwise, false.</returns>
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().AnyAsync(cancellationToken);
    }
}
