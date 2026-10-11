using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

public interface INgoRepository : IGenericRepository<Ngo>
{
    /// <summary>
    /// Asynchronously checks if an NGO already exists in the database.
    /// Throws an exception if an NGO is found.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityAlreadyExistsException">Thrown if an NGO already exists in the database.</exception>
    Task EnsureDoesNotExistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the single NGO entity.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved NGO entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    Task<Ngo> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the first record from the data set and projects it to the target type.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected result (e.g., DTO).</typeparam>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the projected entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the record is not found in the database.</exception>
    Task<TResult> GetAsync<TResult>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves only the logo file name of the single NGO entity.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the logo file name.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    /// <exception cref="EntityFieldNotFoundException">Thrown if the NGO exists but its logo is not set.</exception>
    Task<string> GetLogoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the single NGO entity with change tracking enabled.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved tracked NGO entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    Task<Ngo> GetForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the ID of the single NGO entity.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved NGO ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    Task<Guid> GetIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if there is NGO in the system.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if at least one NGO exists; otherwise, false.</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}
