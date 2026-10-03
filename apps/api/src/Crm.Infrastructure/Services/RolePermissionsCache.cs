using System.Collections.Frozen;
using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Provides an in-memory implementation of <see cref="IRolePermissionsCache"/>.
/// Utilizes <see cref="IMemoryCache"/> for ultra-fast, local authorization checks,
/// with a built-in fallback mechanism to securely resolve cache misses using transient dependency scopes.
/// </summary>
public sealed partial class RolePermissionsCache(
    IMemoryCache memoryCache,
    IServiceScopeFactory scopeFactory,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RolePermissionsCache> logger) : IRolePermissionsCache, IDisposable
{
    private const string CacheKeyPrefix = "RoleAccessRights_";
    private const int LockBucketCount = 16;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(CacheConstants.RolePermissionsCacheExpirationHours);
    private readonly TimeSpan _negativeCacheTtl = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenExpiryMinutes);
    private readonly SemaphoreSlim[] _locks = [.. Enumerable.Range(0, LockBucketCount).Select(_ => new SemaphoreSlim(1, 1))];
    private int _disposed;

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
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
        => SetRolePermissionsAsync(roleId, rights, CacheExpiration);

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
        var roleRepository = scope.ServiceProvider.GetRequiredService<IAuthRoleRepository>();

        var rolesDictionary = await roleRepository.GetAllRolesPermissionsAsync(cancellationToken);

        int seededCount = 0;

        foreach (var kvp in rolesDictionary)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await SetRolePermissionsAsync(kvp.Key, kvp.Value, cancellationToken);
            seededCount++;
        }

        LogCacheSeededSuccessfully(logger, seededCount);
    }

    /// <summary>
    /// Disposes all underlying semaphore instances to prevent native resource leaks.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

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
    private bool TryGetCachedPermissions(string cacheKey, out FrozenSet<string>? rights)
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
    private async Task<IReadOnlySet<string>> FetchAndCacheRoleAccessRightsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IRoleIdentityService>();

        var rights = new HashSet<string>();

        try
        {
            rights = await identityService.GetRolePermissionsAsync(roleId, cancellationToken);

            await SetRolePermissionsAsync(roleId, rights, cancellationToken);
        }
        catch (EntityNotFoundException)
        {
            await SetRolePermissionsAsync(roleId, rights, _negativeCacheTtl);
        }

        return rights;
    }

    /// <summary>
    /// Sets role permissions in the cache with a custom expiration time.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="rights">The set of access rights to cache.</param>
    /// <param name="cacheExpiration">The custom cache expiration timespan.</param>
    private Task SetRolePermissionsAsync(Guid roleId, HashSet<string> rights, TimeSpan cacheExpiration)
    {
        memoryCache.Set(GenerateCacheKey(roleId), rights.ToFrozenSet(), cacheExpiration);

        LogAccessRightsUpdated(logger, roleId, rights.Count);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a standardized, collision-resistant key for the underlying caching provider.
    /// </summary>
    private static string GenerateCacheKey(Guid roleId) => $"{CacheKeyPrefix}{roleId}";

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
