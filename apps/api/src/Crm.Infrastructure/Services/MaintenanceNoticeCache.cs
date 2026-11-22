using System.Diagnostics.CodeAnalysis;
using Crm.Application.Dtos.System;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Caches the scheduled maintenance notice in memory for up to
/// <see cref="CacheConstants.MaintenanceNoticeCacheExpirationSeconds"/> seconds,
/// while expiring notices immediately once <see cref="MaintenanceNoticeDto.EndsAt"/> has passed.
/// </summary>
public sealed class MaintenanceNoticeCache(
    IMemoryCache memoryCache,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IMaintenanceNoticeCache, IDisposable
{
    private const string CacheKey = "SystemMaintenanceNotice";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromSeconds(CacheConstants.MaintenanceNoticeCacheExpirationSeconds);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _disposed;

    /// <inheritdoc />
    public async Task<MaintenanceNoticeDto?> GetActiveNoticeAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        if (TryGetFreshEntry(now, out var cached))
        {
            return FilterActive(cached.Notice, now.UtcDateTime);
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();

            if (TryGetFreshEntry(now, out cached))
            {
                return FilterActive(cached.Notice, now.UtcDateTime);
            }

            var notice = await FetchNoticeAsync(cancellationToken);
            memoryCache.Set(CacheKey, new CachedMaintenanceNotice(notice, now), CacheExpiration);

            return FilterActive(notice, now.UtcDateTime);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lock.Dispose();
    }

    private bool TryGetFreshEntry(DateTimeOffset now, [NotNullWhen(true)] out CachedMaintenanceNotice? entry)
    {
        if (memoryCache.TryGetValue(CacheKey, out CachedMaintenanceNotice? cached)
            && cached is not null
            && now - cached.CachedAt < CacheExpiration)
        {
            entry = cached;
            return true;
        }

        entry = null;
        return false;
    }

    private async Task<MaintenanceNoticeDto?> FetchNoticeAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMaintenanceNoticeRepository>();

        var entity = await repository.GetAsync(cancellationToken);

        return entity is null
            ? null
            : new MaintenanceNoticeDto(
                entity.Message,
                DateTime.SpecifyKind(entity.StartsAt, DateTimeKind.Utc),
                DateTime.SpecifyKind(entity.EndsAt, DateTimeKind.Utc));
    }

    private static MaintenanceNoticeDto? FilterActive(MaintenanceNoticeDto? notice, DateTime utcNow)
        => notice is not null && notice.EndsAt > utcNow
            ? notice
            : null;

    private sealed record CachedMaintenanceNotice(MaintenanceNoticeDto? Notice, DateTimeOffset CachedAt);
}
