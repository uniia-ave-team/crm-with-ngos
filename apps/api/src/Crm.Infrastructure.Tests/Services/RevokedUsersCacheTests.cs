using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RevokedUsersCache"/> covering access-token-aligned TTL expiration,
/// unrevocation on reactivation, and user isolation.
/// </summary>
public class RevokedUsersCacheTests
{
    [Fact]
    public async Task RevokeUserShouldRemainRevokedForAccessTokenLifetimeAndExpireAfterward()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.Zero));
        var jwtOptions = MsOptions.Create(new JwtOptions
        {
            Secret = "super-secret-signing-key-for-tests-1234",
            Issuer = "CrmTestIssuer",
            Audience = "CrmTestAudience",
            AccessTokenExpiryMinutes = 15,
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new RevokedUsersCache(memoryCache, jwtOptions, clock, NullLogger<RevokedUsersCache>.Instance);
        var revokedUserId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();

        (await cache.IsUserRevokedAsync(revokedUserId, ct)).ShouldBeFalse();

        await cache.RevokeUserAsync(revokedUserId, ct);

        (await cache.IsUserRevokedAsync(revokedUserId, ct)).ShouldBeTrue();
        (await cache.IsUserRevokedAsync(otherUserId, ct)).ShouldBeFalse();

        // Still revoked right before access token expiry.
        clock.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
        (await cache.IsUserRevokedAsync(revokedUserId, ct)).ShouldBeTrue();

        // Expires once the full access token lifetime has elapsed.
        clock.Advance(TimeSpan.FromSeconds(1));
        (await cache.IsUserRevokedAsync(revokedUserId, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task UnrevokeUserShouldRemoveUserImmediatelyBeforeTtlExpires()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.Zero));
        var jwtOptions = MsOptions.Create(new JwtOptions
        {
            Secret = "super-secret-signing-key-for-tests-1234",
            Issuer = "CrmTestIssuer",
            Audience = "CrmTestAudience",
            AccessTokenExpiryMinutes = 15,
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new RevokedUsersCache(memoryCache, jwtOptions, clock, NullLogger<RevokedUsersCache>.Instance);
        var userId = Guid.CreateVersion7();

        await cache.RevokeUserAsync(userId, ct);
        (await cache.IsUserRevokedAsync(userId, ct)).ShouldBeTrue();

        await cache.UnrevokeUserAsync(userId, ct);
        (await cache.IsUserRevokedAsync(userId, ct)).ShouldBeFalse();
    }

    private sealed class TestTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
