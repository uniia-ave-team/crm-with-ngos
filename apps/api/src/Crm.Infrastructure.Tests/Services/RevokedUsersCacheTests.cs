using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
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

        using var serviceProvider = BuildServiceProvider();
        var cache = new RevokedUsersCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            jwtOptions,
            clock,
            NullLogger<RevokedUsersCache>.Instance);
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

        using var serviceProvider = BuildServiceProvider();
        var cache = new RevokedUsersCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            jwtOptions,
            clock,
            NullLogger<RevokedUsersCache>.Instance);
        var userId = Guid.CreateVersion7();

        await cache.RevokeUserAsync(userId, ct);
        (await cache.IsUserRevokedAsync(userId, ct)).ShouldBeTrue();

        await cache.UnrevokeUserAsync(userId, ct);
        (await cache.IsUserRevokedAsync(userId, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeTokensIssuedBeforeShouldRejectOnlyOlderTokensAndPreserveFullRevocation()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(start);
        var jwtOptions = MsOptions.Create(new JwtOptions
        {
            Secret = "super-secret-signing-key-for-tests-1234",
            Issuer = "CrmTestIssuer",
            Audience = "CrmTestAudience",
            AccessTokenExpiryMinutes = 15,
        });

        using var serviceProvider = BuildServiceProvider();
        var cache = new RevokedUsersCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            jwtOptions,
            clock,
            NullLogger<RevokedUsersCache>.Instance);
        var userId = Guid.CreateVersion7();

        var oldTokenIssuedAt = start;
        clock.Advance(TimeSpan.FromMilliseconds(50));

        // Role change happens at start + 50ms.
        await cache.RevokeTokensIssuedBeforeAsync(userId, ct);

        // User is NOT fully revoked, but tokens issued at or before the role change are rejected.
        (await cache.IsUserRevokedAsync(userId, ct)).ShouldBeFalse();
        (await cache.IsTokenRevokedAsync(userId, oldTokenIssuedAt, ct)).ShouldBeTrue();
        (await cache.IsTokenRevokedAsync(userId, clock.GetUtcNow(), ct)).ShouldBeTrue();
        (await cache.IsTokenRevokedAsync(userId, null, ct)).ShouldBeTrue();

        // Newly refreshed token issued even 1ms later is accepted immediately.
        var freshTokenIssuedAt = clock.GetUtcNow().AddMilliseconds(1);
        (await cache.IsTokenRevokedAsync(userId, freshTokenIssuedAt, ct)).ShouldBeFalse();

        // After TTL expires, cache entry clears and old checks return false.
        clock.Advance(TimeSpan.FromMinutes(15));
        (await cache.IsTokenRevokedAsync(userId, oldTokenIssuedAt, ct)).ShouldBeFalse();

        // Full revocation must NOT be downgraded by a subsequent role change.
        await cache.RevokeUserAsync(userId, ct);
        clock.Advance(TimeSpan.FromSeconds(1));
        await cache.RevokeTokensIssuedBeforeAsync(userId, ct);

        (await cache.IsUserRevokedAsync(userId, ct)).ShouldBeTrue();
        (await cache.IsTokenRevokedAsync(userId, clock.GetUtcNow().AddSeconds(10), ct)).ShouldBeTrue();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private sealed class TestTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
