using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.BackgroundServices;

/// <summary>
/// Background service responsible for periodically removing expired refresh tokens from the database.
/// Ensures the token storage does not grow indefinitely.
/// </summary>
public partial class ExpiredTokensCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredTokensCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogCleanupServiceStarting(logger);

        using var timer = new PeriodicTimer(_cleanupInterval);

        try
        {
            await CleanupTokensAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupTokensAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            LogCleanupServiceStopping(logger);
        }
        catch (Exception ex)
        {
            LogCleanupServiceFatalError(logger, ex);
        }
    }

    private async Task CleanupTokensAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogCleanupStarted(logger);

            using var scope = scopeFactory.CreateScope();
            var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IUserRefreshTokenRepository>();

            await refreshTokenRepository.RemoveExpiredTokensAsync(cancellationToken);

            LogCleanupSuccessfullyFinished(logger);
        }
        catch (Exception ex)
        {
            LogCleanupFailed(logger, ex);
        }
    }

    [LoggerMessage(EventId = LogEventIds.CleanupServiceStarting, Level = LogLevel.Information, Message = "Expired tokens cleanup background service is starting.")]
    private static partial void LogCleanupServiceStarting(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupServiceStopping, Level = LogLevel.Information, Message = "Expired tokens cleanup background service is stopping.")]
    private static partial void LogCleanupServiceStopping(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupServiceFatalError, Level = LogLevel.Critical, Message = "A fatal error occurred in the expired tokens cleanup service.")]
    private static partial void LogCleanupServiceFatalError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = LogEventIds.CleanupStarted, Level = LogLevel.Information, Message = "Starting expired refresh tokens cleanup...")]
    private static partial void LogCleanupStarted(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupSuccessfullyFinished, Level = LogLevel.Information, Message = "Successfully finished expired refresh tokens cleanup.")]
    private static partial void LogCleanupSuccessfullyFinished(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupFailed, Level = LogLevel.Error, Message = "Failed to clean up expired refresh tokens from the database.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
