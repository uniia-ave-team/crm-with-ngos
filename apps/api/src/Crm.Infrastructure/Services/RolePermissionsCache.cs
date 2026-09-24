using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Identity.Caching;

/// <summary>
/// Provides an in-memory implementation of <see cref="IRolePermissionsCache"/>.
/// Utilizes <see cref="IMemoryCache"/> for ultra-fast, local authorization checks,
/// with a built-in fallback mechanism to securely resolve cache misses using transient dependency scopes.
/// </summary>
public sealed partial class RolePermissionsCache(
    IMemoryCache memoryCache,
    IServiceScopeFactory scopeFactory,
    ILogger<RolePermissionsCache> logger) : IRolePermissionsCache, IDisposable
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim[] _locks = [.. Enumerable.Range(0, 16).Select(_ => new SemaphoreSlim(1, 1))];

    /// <inheritdoc />
    public async Task<HashSet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        string cacheKey = GenerateCacheKey(roleId);

        if (TryGetCachedPermissions(cacheKey, out var cachedRights))
        {
            return cachedRights;
        }

        var lockIndex = (roleId.GetHashCode() & int.MaxValue) % _locks.Length;
        var myLock = _locks[lockIndex];

        await myLock.WaitAsync(cancellationToken);

        try
        {
            if (TryGetCachedPermissions(cacheKey, out cachedRights))
            {
                return cachedRights;
            }

            LogCacheMiss(logger, roleId);
            return await FetchAndCacheRoleAccessRightsAsync(roleId, cancellationToken);
        }
        finally
        {
            myLock.Release();
        }
    }

    /// <inheritdoc />
    public Task SetRolePermissionsAsync(Guid roleId, HashSet<string> rights, CancellationToken cancellationToken = default)
    {
        string cacheKey = GenerateCacheKey(roleId);

        memoryCache.Set(cacheKey, rights, CacheExpiration);

        LogAccessRightsUpdated(logger, roleId, rights.Count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        string cacheKey = GenerateCacheKey(roleId);

        memoryCache.Remove(cacheKey);

        LogAccessRightsRemoved(logger, roleId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SeedAllRolesCacheAsync(CancellationToken cancellationToken = default)
    {
        LogSeedingCache(logger);

        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AuthRole>>();

        var roles = await roleManager.Roles.ToListAsync(cancellationToken);
        int seededCount = 0;

        foreach (var role in roles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claims = await roleManager.GetClaimsAsync(role);
            var rights = claims
                .Where(c => c.Type == PermissionExtensions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet();

            await SetRolePermissionsAsync(role.Id, rights, cancellationToken);
            seededCount++;
        }

        LogCacheSeededSuccessfully(logger, seededCount);
    }

    /// <summary>
    /// Disposes all underlying semaphore instances to prevent native resource leaks.
    /// </summary>
    public void Dispose()
    {
        foreach (var semaphore in _locks)
        {
            semaphore.Dispose();
        }
    }

    /// <summary>
    /// Attempts to retrieve cached permissions for a given role ID from the in-memory cache.
    /// </summary>
    /// <param name="cacheKey">The key used to identify the cached permissions.</param>
    /// <param name="rights">When the method returns, contains the cached permissions, or null if not found.</param>
    /// <returns>true if the permissions were found in the cache; otherwise, false.</returns>
    private bool TryGetCachedPermissions(string cacheKey, out HashSet<string>? rights)
    {
        if (memoryCache.TryGetValue(cacheKey, out rights) && rights != null)
        {
            return true;
        }

        rights = null;
        return false;
    }

    /// <summary>
    /// Acts as a fallback mechanism to safely retrieve a role's access rights from the database
    /// if they were evicted from the cache or missed during the initial seeding process.
    /// </summary>
    /// <param name="roleId">The identifier of the role to resolve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A newly cached set of access right claims for the specified role.</returns>
    private async Task<HashSet<string>> FetchAndCacheRoleAccessRightsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AuthRole>>();

        var role = await roleManager.FindByIdAsync(roleId.ToString());
        var rights = new HashSet<string>();

        if (role != null)
        {
            var claims = await roleManager.GetClaimsAsync(role);
            rights = [.. claims
                .Where(c => c.Type == PermissionExtensions.ClaimType)
                .Select(c => c.Value)];

            await SetRolePermissionsAsync(roleId, rights, cancellationToken);
        }

        return rights;
    }

    /// <summary>
    /// Generates a standardized, collision-resistant key for the underlying caching provider.
    /// </summary>
    private static string GenerateCacheKey(Guid roleId) => $"RoleAccessRights_{roleId}";

    [LoggerMessage(EventId = LogEventIds.CacheMiss, Level = LogLevel.Warning, Message = "Cache miss for role ID '{RoleId}'. Fetching from database as fallback.")]
    private static partial void LogCacheMiss(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AccessRightsUpdated, Level = LogLevel.Information, Message = "Access rights cache updated for role ID '{RoleId}' with {Count} rights.")]
    private static partial void LogAccessRightsUpdated(ILogger logger, Guid roleId, int count);

    [LoggerMessage(EventId = LogEventIds.AccessRightsRemoved, Level = LogLevel.Information, Message = "Access rights cache removed for role ID '{RoleId}'.")]
    private static partial void LogAccessRightsRemoved(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.SeedingCache, Level = LogLevel.Information, Message = "Starting to seed all roles into access rights cache.")]
    private static partial void LogSeedingCache(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CacheSeededSuccessfully, Level = LogLevel.Information, Message = "Successfully seeded {Count} roles into the access rights cache.")]
    private static partial void LogCacheSeededSuccessfully(ILogger logger, int count);
}
