using System.ComponentModel;
using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services.Caching;

/// <summary>
/// Provides a <see cref="HybridCache"/>-backed implementation of <see cref="IRevokedUsersCache"/>.
/// Stores revoked user entries with expiration aligned to <see cref="JwtOptions.AccessTokenExpiryMinutes"/>
/// so deactivated or deleted users lose API access immediately without querying the database on every request.
/// </summary>
/// <param name="hybridCache">The hybrid cache backing store.</param>
/// <param name="jwtOptions">The JWT options providing the access token lifetime used as cache TTL.</param>
/// <param name="timeProvider">The time provider controlling deterministic entry expiration checks.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class RevokedUsersCache(
    HybridCache hybridCache,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<RevokedUsersCache> logger) : IRevokedUsersCache
{
    private const string CacheKeyPrefix = "RevokedUser_";

    private static readonly HybridCacheEntryOptions ReadOnlyLookupOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData
            | HybridCacheEntryFlags.DisableLocalCacheWrite
            | HybridCacheEntryFlags.DisableDistributedCacheWrite,
    };

    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenExpiryMinutes);

    private readonly HybridCacheEntryOptions _writeOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenExpiryMinutes),
        LocalCacheExpiration = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenExpiryMinutes),
    };

    /// <inheritdoc />
    public async Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        DateTimeOffset expiresAt = timeProvider.GetUtcNow().Add(_ttl);
        await hybridCache.SetAsync(
            GenerateCacheKey(userId),
            new RevokedUserEntry(expiresAt),
            _writeOptions,
            cancellationToken: cancellationToken);

        LogUserAddedToRevokedCache(logger, userId, _ttl);
    }

    /// <inheritdoc />
    public async Task UnrevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveAsync(GenerateCacheKey(userId), cancellationToken);
        LogUserRemovedFromRevokedCache(logger, userId);
    }

    /// <inheritdoc />
    public async Task<bool> IsUserRevokedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        string key = GenerateCacheKey(userId);
        var entry = await hybridCache.GetOrCreateAsync(
            key,
            static _ => ValueTask.FromResult<RevokedUserEntry?>(null),
            ReadOnlyLookupOptions,
            cancellationToken: cancellationToken);

        if (entry is null)
        {
            return false;
        }

        if (entry.ExpiresAt > timeProvider.GetUtcNow())
        {
            return true;
        }

        await hybridCache.RemoveAsync(key, cancellationToken);
        return false;
    }

    private static string GenerateCacheKey(Guid userId) => $"{CacheKeyPrefix}{userId}";

    [LoggerMessage(EventId = LogEventIds.UserAddedToRevokedCache, Level = LogLevel.Information, Message = "User ID '{UserId}' added to revoked users cache for {Duration}.")]
    private static partial void LogUserAddedToRevokedCache(ILogger logger, Guid userId, TimeSpan duration);

    [LoggerMessage(EventId = LogEventIds.UserRemovedFromRevokedCache, Level = LogLevel.Information, Message = "User ID '{UserId}' removed from revoked users cache.")]
    private static partial void LogUserRemovedFromRevokedCache(ILogger logger, Guid userId);

    [ImmutableObject(true)]
    private sealed record RevokedUserEntry(DateTimeOffset ExpiresAt);
}
