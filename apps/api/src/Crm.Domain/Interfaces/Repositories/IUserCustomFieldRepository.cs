using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines the contract for the repository managing <see cref="UserCustomField"/> entities.
/// Inherits standard data access operations from <see cref="IGenericRepository{T}"/>.
/// </summary>
public interface IUserCustomFieldRepository : IGenericRepository<UserCustomField>
{
    /// <summary>
    /// Asynchronously checks if a custom field with the specified key already exists for the user.
    /// Throws an exception if the key is found.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="key">The key to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="EntityAlreadyExistsException">Thrown if the key already exists for the user.</exception>
    Task EnsureKeyDoesNotExistAsync(Guid userId, string key, CancellationToken cancellationToken = default);
}
