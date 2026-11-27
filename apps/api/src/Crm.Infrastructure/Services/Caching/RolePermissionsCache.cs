using System.Collections.Frozen;
using System.ComponentModel;
using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services.Caching;

/// <summary>
/// Provides a <see cref="HybridCache"/>-backed implementation of <see cref="IRolePermissionsCache"/>
/// with built-in per-key stampede protection, zero-copy immutable L1 caching, and negative caching for unknown roles.
/// </summary>
/// <param name="hybridCache">The hybrid cache backing store.</param>
/// <param name="scopeFactory">The DI scope factory used for fallback database lookups.</param>
/// <param name="jwtOptions">The JWT options supplying negative-cache TTL aligned with access token lifetime.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class RolePermissionsCache(
    HybridCache hybridCache,
    IServiceScopeFactory scopeFactory,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RolePermissionsCache> logger) : IRolePermissionsCache
{
    private const string CacheKeyPrefix = "RoleAccessRights_";

    private static readonly HybridCacheEntryOptions DefaultEntryOptions = new()
    {
        Expiration = TimeSpan.FromHours(CacheConstants.RolePermissionsCacheExpirationHours),
        LocalCacheExpiration = TimeSpan.FromHours(CacheConstants.RolePermissionsCacheExpirationHours),
    };

    private readonly HybridCacheEntryOptions _negativeCacheEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenExpiryMinutes),
        LocalCacheExpiration = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenExpiryMinutes),
    };

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        string cacheKey = GenerateCacheKey(roleId);

        var entry = await hybridCache.GetOrCreateAsync(
            cacheKey,
            (Cache: this, RoleId: roleId),
            static (state, ct) => state.Cache.FetchRolePermissionsEntryAsync(state.RoleId, ct),
            DefaultEntryOptions,
            cancellationToken: cancellationToken);

        if (entry.IsNotFound)
        {
            await hybridCache.SetAsync(
                cacheKey,
                entry with { IsNotFound = false },
                _negativeCacheEntryOptions,
                cancellationToken: cancellationToken);
        }

        return entry.Rights;
    }

    /// <inheritdoc />
    public async Task SetRolePermissionsAsync(Guid roleId, HashSet<string> rights, CancellationToken cancellationToken = default)
    {
        var frozenRights = rights.ToFrozenSet(StringComparer.Ordinal);
        await hybridCache.SetAsync(
            GenerateCacheKey(roleId),
            new CachedRolePermissions(frozenRights, IsNotFound: false),
            DefaultEntryOptions,
            cancellationToken: cancellationToken);

        LogAccessRightsUpdated(logger, roleId, frozenRights.Count);
    }

    /// <inheritdoc />
    public async Task RemoveRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveAsync(GenerateCacheKey(roleId), cancellationToken);
        LogAccessRightsRemoved(logger, roleId);
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

    private async ValueTask<CachedRolePermissions> FetchRolePermissionsEntryAsync(Guid roleId, CancellationToken cancellationToken)
    {
        LogCacheMiss(logger, roleId);

        await using var scope = scopeFactory.CreateAsyncScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IRoleIdentityService>();

        try
        {
            var rights = await identityService.GetRolePermissionsAsync(roleId, cancellationToken);
            var frozenRights = rights.ToFrozenSet(StringComparer.Ordinal);

            LogAccessRightsUpdated(logger, roleId, frozenRights.Count);
            return new CachedRolePermissions(frozenRights, IsNotFound: false);
        }
        catch (NotFoundException)
        {
            LogAccessRightsUpdated(logger, roleId, 0);
            return new CachedRolePermissions([], IsNotFound: true);
        }
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

    [ImmutableObject(true)]
    private sealed record CachedRolePermissions(FrozenSet<string> Rights, bool IsNotFound);
}
