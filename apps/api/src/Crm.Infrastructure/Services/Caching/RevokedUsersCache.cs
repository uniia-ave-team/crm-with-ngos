using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services.Caching;

/// <summary>
/// Provides an in-memory implementation of <see cref="IRevokedUsersCache"/> backed by <see cref="IMemoryCache"/>.
/// Stores revoked user entries with expiration aligned to <see cref="JwtOptions.AccessTokenExpiryMinutes"/>
/// so deactivated or deleted users lose API access immediately without querying the database on every request.
/// </summary>
/// <param name="memoryCache">The in-memory cache backing store.</param>
/// <param name="jwtOptions">The JWT options providing the access token lifetime used as cache TTL.</param>
/// <param name="timeProvider">The time provider controlling deterministic entry expiration checks.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class RevokedUsersCache(
    IMemoryCache memoryCache,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<RevokedUsersCache> logger) : IRevokedUsersCache
{
    private const string CacheKeyPrefix = "RevokedUser_";
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenExpiryMinutes);

    /// <inheritdoc />
    public Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset expiresAt = timeProvider.GetUtcNow().Add(_ttl);
        memoryCache.Set(GenerateCacheKey(userId), expiresAt, _ttl);

        LogUserAddedToRevokedCache(logger, userId, _ttl);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnrevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        memoryCache.Remove(GenerateCacheKey(userId));

        LogUserRemovedFromRevokedCache(logger, userId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsUserRevokedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string key = GenerateCacheKey(userId);
        if (memoryCache.TryGetValue(key, out DateTimeOffset expiresAt))
        {
            if (expiresAt > timeProvider.GetUtcNow())
            {
                return Task.FromResult(true);
            }

            memoryCache.Remove(key);
        }

        return Task.FromResult(false);
    }

    private static string GenerateCacheKey(Guid userId) => $"{CacheKeyPrefix}{userId}";

    [LoggerMessage(EventId = LogEventIds.UserAddedToRevokedCache, Level = LogLevel.Information, Message = "User ID '{UserId}' added to revoked users cache for {Duration}.")]
    private static partial void LogUserAddedToRevokedCache(ILogger logger, Guid userId, TimeSpan duration);

    [LoggerMessage(EventId = LogEventIds.UserRemovedFromRevokedCache, Level = LogLevel.Information, Message = "User ID '{UserId}' removed from revoked users cache.")]
    private static partial void LogUserRemovedFromRevokedCache(ILogger logger, Guid userId);
}
