using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services.Caching;

/// <summary>
/// Provides an in-memory implementation of <see cref="IRolePermissionsCache"/>.
/// Utilizes <see cref="IMemoryCache"/> for ultra-fast, local authorization checks,
/// with a built-in fallback mechanism to securely resolve cache misses using isolated async scopes.
/// </summary>
/// <param name="memoryCache">The in-memory cache backing store.</param>
/// <param name="scopeFactory">The DI scope factory used for fallback database lookups.</param>
/// <param name="jwtOptions">The JWT options supplying negative-cache TTL aligned with access token lifetime.</param>
/// <param name="logger">The logger instance.</param>
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

        var myLock = _locks[GetLockIndex(roleId)];

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
    public async Task SetRolePermissionsAsync(Guid roleId, HashSet<string> rights, CancellationToken cancellationToken = default)
    {
        var myLock = _locks[GetLockIndex(roleId)];

        await myLock.WaitAsync(cancellationToken);

        try
        {
            CacheRolePermissions(roleId, rights, CacheExpiration);
        }
        finally
        {
            myLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var myLock = _locks[GetLockIndex(roleId)];

        await myLock.WaitAsync(cancellationToken);

        try
        {
            string cacheKey = GenerateCacheKey(roleId);

            memoryCache.Remove(cacheKey);

            LogAccessRightsRemoved(logger, roleId);
        }
        finally
        {
            myLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SeedAllRolesCacheAsync(CancellationToken cancellationToken = default)
    {
        LogSeedingCache(logger);

        await using var scope = scopeFactory.CreateAsyncScope();
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

    private int GetLockIndex(Guid roleId) => (roleId.GetHashCode() & int.MaxValue) % _locks.Length;

    private bool TryGetCachedPermissions(string cacheKey, [NotNullWhen(true)] out FrozenSet<string>? rights)
    {
        if (memoryCache.TryGetValue(cacheKey, out rights) && rights is not null)
        {
            return true;
        }

        rights = null;
        return false;
    }

    private async Task<FrozenSet<string>> FetchAndCacheRoleAccessRightsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IRoleIdentityService>();

        try
        {
            var rights = await identityService.GetRolePermissionsAsync(roleId, cancellationToken);

            return CacheRolePermissions(roleId, rights, CacheExpiration);
        }
        catch (NotFoundException)
        {
            return CacheRolePermissions(roleId, [], _negativeCacheTtl);
        }
    }

    private FrozenSet<string> CacheRolePermissions(Guid roleId, HashSet<string> rights, TimeSpan cacheExpiration)
    {
        var frozenRights = rights.ToFrozenSet(StringComparer.Ordinal);

        memoryCache.Set(GenerateCacheKey(roleId), frozenRights, cacheExpiration);

        LogAccessRightsUpdated(logger, roleId, frozenRights.Count);

        return frozenRights;
    }

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
