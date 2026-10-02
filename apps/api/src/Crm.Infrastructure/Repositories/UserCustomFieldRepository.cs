using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Implements the repository for managing <see cref="UserCustomField"/> entities.
/// Inherits standard data access operations from <see cref="GenericRepository{T}"/>.
/// </summary>
public class UserCustomFieldRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<UserCustomField>(appDbContext),
    IUserCustomFieldRepository
{
    /// <summary>
    /// Asynchronously checks if a custom field with the specified key already exists for the user.
    /// Throws an exception if the key is found.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="key">The key to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="EntityAlreadyExistsException">Thrown if the key already exists for the user.</exception>
    public async Task EnsureKeyDoesNotExistAsync(Guid userId, string key, CancellationToken cancellationToken = default)
    {
        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(cf => cf.UserId == userId && cf.Key == key, cancellationToken);

        if (exists)
        {
            throw new EntityAlreadyExistsException(nameof(UserCustomField), key);
        }
    }
}
