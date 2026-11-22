using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MaintenanceNoticeCache"/> covering 30-second caching and automatic expiration at <see cref="MaintenanceNotice.EndsAt"/>.
/// </summary>
public class MaintenanceNoticeCacheTests
{
    [Fact]
    public async Task GetActiveNoticeShouldCacheNoticeForThirtySecondsAndRefreshAfterward()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero));
        var repository = Substitute.For<IMaintenanceNoticeRepository>();

        var firstNotice = new MaintenanceNotice().Update(
            "First notice",
            new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc),
            clock.GetUtcNow().UtcDateTime);
        var updatedNotice = new MaintenanceNotice().Update(
            "Updated notice",
            new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 23, 30, 0, DateTimeKind.Utc),
            clock.GetUtcNow().UtcDateTime);

        repository.GetAsync(ct).Returns(firstNotice, updatedNotice);

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var serviceProvider = BuildServiceProvider(repository);
        using var cache = new MaintenanceNoticeCache(
            memoryCache,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            clock);

        var initial = await cache.GetActiveNoticeAsync(ct);
        initial.ShouldNotBeNull();
        initial.Message.ShouldBe("First notice");

        clock.Advance(TimeSpan.FromSeconds(29));
        var stillCached = await cache.GetActiveNoticeAsync(ct);
        stillCached.ShouldNotBeNull();
        stillCached.Message.ShouldBe("First notice");
        await repository.Received(1).GetAsync(ct);

        clock.Advance(TimeSpan.FromSeconds(1));
        var refreshed = await cache.GetActiveNoticeAsync(ct);
        refreshed.ShouldNotBeNull();
        refreshed.Message.ShouldBe("Updated notice");
        await repository.Received(2).GetAsync(ct);
    }

    [Fact]
    public async Task GetActiveNoticeShouldReturnNullImmediatelyAfterEndsAtEvenWhileCached()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 20, 22, 59, 50, TimeSpan.Zero));
        var repository = Substitute.For<IMaintenanceNoticeRepository>();

        repository.GetAsync(ct).Returns(new MaintenanceNotice().Update(
            "Ending soon",
            new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc),
            clock.GetUtcNow().UtcDateTime));

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var serviceProvider = BuildServiceProvider(repository);
        using var cache = new MaintenanceNoticeCache(
            memoryCache,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            clock);

        var beforeEnd = await cache.GetActiveNoticeAsync(ct);
        beforeEnd.ShouldNotBeNull();
        beforeEnd.Message.ShouldBe("Ending soon");

        // Advance 10 seconds to exact EndsAt (still inside 30s cache window).
        clock.Advance(TimeSpan.FromSeconds(10));
        var atEnd = await cache.GetActiveNoticeAsync(ct);
        atEnd.ShouldBeNull();

        // Cache hit without re-querying DB.
        await repository.Received(1).GetAsync(ct);
    }

    [Fact]
    public async Task GetActiveNoticeShouldCacheNullAndRefreshAfterThirtySeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero));
        var repository = Substitute.For<IMaintenanceNoticeRepository>();

        repository.GetAsync(ct).Returns(
            null,
            new MaintenanceNotice().Update(
                "New notice",
                new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc),
                clock.GetUtcNow().UtcDateTime));

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var serviceProvider = BuildServiceProvider(repository);
        using var cache = new MaintenanceNoticeCache(
            memoryCache,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            clock);

        (await cache.GetActiveNoticeAsync(ct)).ShouldBeNull();

        clock.Advance(TimeSpan.FromSeconds(15));
        (await cache.GetActiveNoticeAsync(ct)).ShouldBeNull();
        await repository.Received(1).GetAsync(ct);

        clock.Advance(TimeSpan.FromSeconds(15));
        var loaded = await cache.GetActiveNoticeAsync(ct);
        loaded.ShouldNotBeNull();
        loaded.Message.ShouldBe("New notice");
        await repository.Received(2).GetAsync(ct);
    }

    private static ServiceProvider BuildServiceProvider(IMaintenanceNoticeRepository repository)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repository);
        return services.BuildServiceProvider();
    }

    private sealed class TestTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
