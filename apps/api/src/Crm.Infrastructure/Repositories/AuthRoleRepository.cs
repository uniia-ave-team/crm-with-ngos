using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="AuthRole"/> entities.
/// </summary>
public class AuthRoleRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<AuthRole>(appDbContext),
      IAuthRoleRepository
{
    /// <summary>
    /// Asynchronously retrieves all roles assigned to a specific user by their unique identifier.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A list of roles assigned to the user.</returns>
    public async Task<List<AuthRole>> GetRolesByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await Context.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == userId)
            .Join(
                DbSet,
                ur => ur.RoleId,
                role => role.Id,
                (ur, role) => role)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Asynchronously retrieves the names of the roles matching the specified identifiers.
    /// Throws an exception containing all missing role IDs if any role is not found.
    /// </summary>
    /// <param name="roleIds">The collection of role identifiers to fetch names for.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A list of role names corresponding to the provided identifiers.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if one or more role IDs do not exist.</exception>
    public async Task<List<string>> GetRoleNamesByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken ct = default)
    {
        var distinctRoleIds = roleIds.Distinct().ToList();
        if (distinctRoleIds.Count == 0)
        {
            return [];
        }

        var roles = await DbSet
            .AsNoTracking()
            .Where(role => distinctRoleIds.Contains(role.Id))
            .Select(role => new { role.Id, role.Name })
            .ToListAsync(ct);

        if (roles.Count != distinctRoleIds.Count)
        {
            var foundIds = roles.Select(r => r.Id).ToHashSet();
            var missingIds = distinctRoleIds
                .Where(id => !foundIds.Contains(id))
                .Cast<object>()
                .ToList();

            throw new EntitiesNotFoundException(nameof(AuthRole), missingIds);
        }

        return [.. roles.Select(r => r.Name!)];
    }
}
