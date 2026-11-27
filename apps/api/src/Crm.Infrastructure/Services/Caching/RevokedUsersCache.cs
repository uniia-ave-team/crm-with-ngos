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
/// so deactivated/deleted users lose API access immediately and users whose roles changed are forced
/// to refresh stale access tokens without querying the database on every request.
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
            new RevokedUserEntry(RevokedBefore: null, ExpiresAt: expiresAt),
            _writeOptions,
            cancellationToken: cancellationToken);

        LogUserAddedToRevokedCache(logger, userId, _ttl);
    }

    /// <inheritdoc />
    public async Task RevokeTokensIssuedBeforeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        RevokedUserEntry? existing = await GetActiveEntryAsync(userId, utcNow, cancellationToken);

        // Preserve full account revocation (deactivated/deleted user) instead of downgrading it to timestamp-only.
        if (existing is { RevokedBefore: null })
        {
            return;
        }

        DateTimeOffset revokedBefore = existing?.RevokedBefore is { } existingCutoff && existingCutoff > utcNow
            ? existingCutoff
            : utcNow;

        await hybridCache.SetAsync(
            GenerateCacheKey(userId),
            new RevokedUserEntry(RevokedBefore: revokedBefore, ExpiresAt: utcNow.Add(_ttl)),
            _writeOptions,
            cancellationToken: cancellationToken);

        LogUserTokensRevokedBefore(logger, userId, revokedBefore, _ttl);
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
        RevokedUserEntry? entry = await GetActiveEntryAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
        return entry is { RevokedBefore: null };
    }

    /// <inheritdoc />
    public async Task<bool> IsTokenRevokedAsync(
        Guid userId,
        DateTimeOffset? tokenIssuedAt,
        CancellationToken cancellationToken = default)
    {
        RevokedUserEntry? entry = await GetActiveEntryAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
        return entry is not null
            && (entry.RevokedBefore is null || tokenIssuedAt is null || tokenIssuedAt.Value <= entry.RevokedBefore.Value);
    }

    private async Task<RevokedUserEntry?> GetActiveEntryAsync(
        Guid userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        string key = GenerateCacheKey(userId);
        var entry = await hybridCache.GetOrCreateAsync(
            key,
            static _ => ValueTask.FromResult<RevokedUserEntry?>(null),
            ReadOnlyLookupOptions,
            cancellationToken: cancellationToken);

        if (entry is null)
        {
            return null;
        }

        if (entry.ExpiresAt > utcNow)
        {
            return entry;
        }

        await hybridCache.RemoveAsync(key, cancellationToken);
        return null;
    }

    private static string GenerateCacheKey(Guid userId) => $"{CacheKeyPrefix}{userId}";

    [LoggerMessage(EventId = LogEventIds.UserAddedToRevokedCache, Level = LogLevel.Information, Message = "User ID '{UserId}' added to revoked users cache for {Duration}.")]
    private static partial void LogUserAddedToRevokedCache(ILogger logger, Guid userId, TimeSpan duration);

    [LoggerMessage(EventId = LogEventIds.UserRemovedFromRevokedCache, Level = LogLevel.Information, Message = "User ID '{UserId}' removed from revoked users cache.")]
    private static partial void LogUserRemovedFromRevokedCache(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserTokensRevokedBefore, Level = LogLevel.Information, Message = "Access tokens issued at or before {RevokedBefore:O} invalidated for user ID '{UserId}' for {Duration}.")]
    private static partial void LogUserTokensRevokedBefore(ILogger logger, Guid userId, DateTimeOffset revokedBefore, TimeSpan duration);

    [ImmutableObject(true)]
    private sealed record RevokedUserEntry(DateTimeOffset? RevokedBefore, DateTimeOffset ExpiresAt);
}
