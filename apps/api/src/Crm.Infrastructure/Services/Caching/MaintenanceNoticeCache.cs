using System.ComponentModel;
using Crm.Application.Dtos.System;
using Crm.Application.Interfaces;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Consts;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Services.Caching;

/// <summary>
/// Caches the scheduled maintenance notice via <see cref="HybridCache"/> for up to
/// <see cref="CacheConstants.MaintenanceNoticeCacheExpirationSeconds"/> seconds (with built-in stampede protection),
/// while expiring notices immediately once <see cref="MaintenanceNoticeDto.EndsAt"/> has passed.
/// </summary>
/// <param name="hybridCache">The hybrid cache backing store.</param>
/// <param name="scopeFactory">The DI scope factory used to query <see cref="IMaintenanceNoticeRepository"/>.</param>
/// <param name="timeProvider">The time provider controlling cache freshness and notice expiration.</param>
public sealed class MaintenanceNoticeCache(
    HybridCache hybridCache,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IMaintenanceNoticeCache
{
    private const string CacheKey = "SystemMaintenanceNotice";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromSeconds(CacheConstants.MaintenanceNoticeCacheExpirationSeconds);

    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = CacheExpiration,
        LocalCacheExpiration = CacheExpiration,
    };

    /// <inheritdoc />
    public async Task<MaintenanceNoticeDto?> GetActiveNoticeAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var cached = await GetOrLoadEntryAsync(cancellationToken);

        if (now - cached.CachedAt >= CacheExpiration)
        {
            await hybridCache.RemoveAsync(CacheKey, cancellationToken);
            cached = await GetOrLoadEntryAsync(cancellationToken);
        }

        return FilterActive(cached.Notice, now.UtcDateTime);
    }

    private ValueTask<CachedMaintenanceNotice> GetOrLoadEntryAsync(CancellationToken cancellationToken)
        => hybridCache.GetOrCreateAsync(
            CacheKey,
            this,
            static (cache, ct) => cache.LoadEntryAsync(ct),
            EntryOptions,
            cancellationToken: cancellationToken);

    private async ValueTask<CachedMaintenanceNotice> LoadEntryAsync(CancellationToken cancellationToken)
    {
        var notice = await FetchNoticeAsync(cancellationToken);
        return new CachedMaintenanceNotice(notice, timeProvider.GetUtcNow());
    }

    private async Task<MaintenanceNoticeDto?> FetchNoticeAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMaintenanceNoticeRepository>();

        var entity = await repository.GetAsync(cancellationToken);

        return entity is null
            ? null
            : new MaintenanceNoticeDto(
                entity.Message,
                entity.StartsAt.ToUtc(),
                entity.EndsAt.ToUtc());
    }

    private static MaintenanceNoticeDto? FilterActive(MaintenanceNoticeDto? notice, DateTime utcNow)
        => notice is not null && notice.EndsAt > utcNow
            ? notice
            : null;

    [ImmutableObject(true)]
    private sealed record CachedMaintenanceNotice(MaintenanceNoticeDto? Notice, DateTimeOffset CachedAt);
}
