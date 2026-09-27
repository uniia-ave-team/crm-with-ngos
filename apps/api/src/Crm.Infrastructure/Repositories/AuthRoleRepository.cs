using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Mapster;
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
    /// Asynchronously checks if an entity with the specified role name exists.
    /// Throws an exception if the entity is not found.
    /// </summary>
    /// <param name="roleName">The name of the role to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if no entity with the specified role name exists.</exception>
    public async Task EnsureExistsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedName = roleName.ToUpperInvariant();

        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(m => m.NormalizedName == normalizedName, cancellationToken);

        if (!exists)
        {
            throw new EntityNotFoundException(nameof(AuthRole), roleName);
        }
    }

    /// <summary>
    /// Asynchronously retrieves the unique identifier of an application role by its name.
    /// </summary>
    /// <param name="roleName">The name of the role to find.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the role's <see cref="Guid"/> identifier.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if no role with the specified name exists.</exception>
    public async Task<Guid> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedName = roleName.ToUpperInvariant();

        var roleId = await DbSet
            .AsNoTracking()
            .Where(m => m.NormalizedName == normalizedName)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return roleId ?? throw new EntityNotFoundException(nameof(AuthRole), roleName);
    }

    /// <summary>
    /// Asynchronously retrieves all roles assigned to a specific user by their unique identifier,
    /// projected to the target type <typeparamref name="TRole"/> using compile-time projection.
    /// </summary>
    /// <typeparam name="TRole">The type of the role DTO/model to project into.</typeparam>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of projected roles assigned to the user.</returns>
    public async Task<List<TRole>> GetRolesByUserAsync<TRole>(Guid userId, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(role => role.UserRoles.Any(ur => ur.UserId == userId))
            .ProjectToType<TRole>()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Asynchronously retrieves the unique identifiers of all roles assigned to a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of role IDs assigned to the user.</returns>
    public async Task<List<Guid>> GetRoleIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<AuthUserRole>()
            .AsNoTracking()
            .Where(aur => aur.UserId == userId)
            .Select(aur => aur.RoleId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected role entities based on the search term.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="searchTerm">Optional search term to filter roles by name, feminitive name, or plural name.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (e.g., "asc" or "desc"). Defaults to ascending.</param>
    /// <param name="pageNumber">The current page number (1-based index). Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paginated collection of projected items along with pagination metadata.</returns>
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => GetPagedAsync<TResult>(
            predicate: BuildRoleFilter(searchTerm),
            orderBy: orderBy,
            sortOrder: sortOrder,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Asynchronously checks if the specified user is the last active administrator in the system.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the user is the last active administrator; otherwise, false.</returns>
    public async Task<bool> IsUserLastAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activeAdminIds = await Context.Set<AuthUserRole>()
            .Where(ur => ur.Role.Name == RoleConsts.Admin && ur.User.UserProfile.IsActive)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return activeAdminIds.Count == 1 && activeAdminIds[0] == userId;
    }

    /// <summary>
    /// Asynchronously retrieves the names of the roles matching the specified identifiers.
    /// Throws an exception containing all missing role IDs if any role is not found.
    /// </summary>
    /// <param name="roleIds">The collection of role identifiers to fetch names for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of role names corresponding to the provided identifiers.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if one or more role IDs do not exist.</exception>
    public async Task<List<string>> GetRoleNamesByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default)
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
            .ToListAsync(cancellationToken);

        if (roles.Count != distinctRoleIds.Count)
        {
            var foundIds = roles.Select(r => r.Id).ToHashSet();
            var missingIds = distinctRoleIds
                .Where(id => !foundIds.Contains(id))
                .Cast<object>()
                .ToList();

            throw new EntitiesNotFoundException(nameof(AuthRole), missingIds);
        }

        return [.. roles.Select(r => r.Name ?? throw new InvalidOperationException($"Role {r.Id} has a null Name."))];
    }

    /// <summary>
    /// Asynchronously retrieves all role permissions from the database, grouped by their respective role identifiers.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <see cref="Dictionary{TKey, TValue}"/>
    /// where the key is the Role ID (<see cref="Guid"/>) and the value is a <see cref="HashSet{T}"/> of permission strings (claims)
    /// associated with that role.
    /// </returns>
    public async Task<Dictionary<Guid, HashSet<string>>> GetAllRolesPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Set<AuthRoleClaim>()
            .Where(c => c.ClaimType == CustomClaimTypes.Permission && c.ClaimValue != null)
            .Select(c => new { c.RoleId, c.ClaimValue })
            .GroupBy(c => c.RoleId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(c => c.ClaimValue!).ToHashSet(),
                cancellationToken);
    }

    /// <summary>
    /// Builds the filter expression for querying roles based on an optional search term.
    /// Matches against the standard name, feminitive name, or plural name.
    /// </summary>
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF Core requires ToLower for correct SQL translation")]
    private static Expression<Func<AuthRole, bool>>? BuildRoleFilter(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        string term = searchTerm.Trim().ToUpperInvariant();

        return role =>
            (role.NormalizedName != null && role.NormalizedName.Contains(term)) ||
            (role.FeminitiveName != null && role.FeminitiveName.ToUpper().Contains(term)) ||
            (role.PluralName != null && role.PluralName.ToUpper().Contains(term));
    }
}
