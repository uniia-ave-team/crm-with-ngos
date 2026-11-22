using Crm.Application.Common.Consts;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically prunes <see cref="AuditLogEntry"/> records older than
/// <see cref="AuditOptions.RetentionDays"/> so the PostgreSQL audit log table remains bounded in size.
/// </summary>
/// <param name="scopeFactory">The DI scope factory used to resolve <see cref="ApplicationDbContext"/>.</param>
/// <param name="options">The auditing configuration options.</param>
/// <param name="timeProvider">The time provider used to calculate the retention cutoff timestamp and schedule ticks.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class AuditLogsCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditLogsCleanupService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int retentionDays = options.Value.RetentionDays;
        if (retentionDays <= 0)
        {
            return;
        }

        LogCleanupServiceStarting(logger, retentionDays);

        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.CleanupIntervalHours));
        using var timer = new PeriodicTimer(interval, timeProvider);

        try
        {
            await CleanupExpiredAuditLogsAsync(retentionDays, stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupExpiredAuditLogsAsync(retentionDays, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogCleanupServiceStopping(logger);
        }
        catch (Exception ex)
        {
            LogCleanupServiceFatalError(logger, ex);
        }
    }

    private async Task CleanupExpiredAuditLogsAsync(int retentionDays, CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset cutoff = timeProvider.GetUtcNow().AddDays(-retentionDays);

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            int deletedCount = await dbContext.Set<AuditLogEntry>()
                .Where(e => e.OccurredAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            LogCleanupCompleted(logger, deletedCount, cutoff);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCleanupFailed(logger, ex);
        }
    }

    [LoggerMessage(EventId = LogEventIds.AuditCleanupServiceStarting, Level = LogLevel.Information, Message = "Audit logs retention cleanup service is starting (retention: {RetentionDays} days).")]
    private static partial void LogCleanupServiceStarting(ILogger logger, int retentionDays);

    [LoggerMessage(EventId = LogEventIds.AuditCleanupServiceStopping, Level = LogLevel.Information, Message = "Audit logs retention cleanup service is stopping.")]
    private static partial void LogCleanupServiceStopping(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.AuditCleanupServiceFatalError, Level = LogLevel.Critical, Message = "A fatal error occurred in the audit logs retention cleanup service.")]
    private static partial void LogCleanupServiceFatalError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = LogEventIds.AuditCleanupCompleted, Level = LogLevel.Information, Message = "Audit logs retention cleanup completed: deleted {DeletedCount} entries older than {CutoffUtc}.")]
    private static partial void LogCleanupCompleted(ILogger logger, int deletedCount, DateTimeOffset cutoffUtc);

    [LoggerMessage(EventId = LogEventIds.AuditCleanupFailed, Level = LogLevel.Error, Message = "Failed to prune expired audit log entries from the database.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
