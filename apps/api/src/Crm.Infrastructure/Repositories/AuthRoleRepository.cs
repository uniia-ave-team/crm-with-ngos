using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="AuthRole"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class AuthRoleRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<AuthRole>(appDbContext),
      IAuthRoleRepository
{
    /// <inheritdoc />
    public async Task EnsureExistsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedName = roleName.ToUpperInvariant();

        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(m => m.NormalizedName == normalizedName, cancellationToken);

        if (!exists)
        {
            throw new EntityNotFoundException<AuthRole>(roleName);
        }
    }

    /// <inheritdoc />
    public async Task<Guid> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
        => await FindRoleIdByNameAsync(roleName, cancellationToken)
           ?? throw new EntityNotFoundException<AuthRole>(roleName);

    /// <inheritdoc />
    public Task<Guid?> FindRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedName = roleName.ToUpperInvariant();

        return DbSet
            .AsNoTracking()
            .Where(m => m.NormalizedName == normalizedName)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<TRole>> GetRolesByUserAsync<TRole>(Guid userId, CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .Where(role => role.UserRoles.Any(ur => ur.UserId == userId))
            .ProjectToType<TRole>()
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ILookup<Guid, TRole>> GetRolesByUserIdsAsync<TRole>(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var distinctUserIds = userIds.Distinct().ToList();
        return distinctUserIds.Count == 0
            ? Enumerable.Empty<TRole>().ToLookup(_ => Guid.Empty)
            : await Context.Set<AuthUserRole>()
                .AsNoTracking()
                .Where(ur => distinctUserIds.Contains(ur.UserId))
                .ToProjectedLookupAsync<AuthUserRole, AuthRole, Guid, TRole>(
                    ur => ur.UserId,
                    ur => ur.Role,
                    cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<Guid>> GetRoleIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Context
            .Set<AuthUserRole>()
            .AsNoTracking()
            .Where(aur => aur.UserId == userId)
            .Select(aur => aur.RoleId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<bool> IsUserLastAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var normalizedAdminName = RoleConstants.Admin.ToUpperInvariant();

        var activeAdminIds = await Context.Set<AuthUserRole>()
            .AsNoTracking()
            .Where(ur =>
                ur.Role.NormalizedName == normalizedAdminName &&
                ur.User.UserProfile.IsActive)
            .Select(ur => ur.UserId)
            .Distinct()
            .OrderBy(id => id)
            .Take(2)
            .ToListAsync(cancellationToken);

        return activeAdminIds.Count == 1 && activeAdminIds[0] == userId;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<Dictionary<Guid, HashSet<string>>> GetAllRolesPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var claims = await Context.Set<AuthRoleClaim>()
            .AsNoTracking()
            .Where(c => c.ClaimType == CustomClaimTypes.Permission && c.ClaimValue != null)
            .Select(c => new { c.RoleId, ClaimValue = c.ClaimValue! })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, HashSet<string>>();
        foreach (var claim in claims)
        {
            if (!result.TryGetValue(claim.RoleId, out var permissions))
            {
                permissions = new HashSet<string>(StringComparer.Ordinal);
                result[claim.RoleId] = permissions;
            }

            permissions.Add(claim.ClaimValue);
        }

        return result;
    }

    private static Expression<Func<AuthRole, bool>>? BuildRoleFilter(string? searchTerm)
        => string.IsNullOrWhiteSpace(searchTerm)
            ? null
            : QueryFilterExpressions.ContainsIgnoreCase<AuthRole>(
                searchTerm,
                role => role.Name,
                role => role.FeminitiveName,
                role => role.PluralName);
}
