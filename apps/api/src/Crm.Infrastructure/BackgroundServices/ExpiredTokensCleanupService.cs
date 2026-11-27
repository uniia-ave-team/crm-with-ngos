using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Consts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.BackgroundServices;

/// <summary>
/// Background service responsible for periodically removing expired refresh tokens and action tokens from the database.
/// Ensures the token storage does not grow indefinitely.
/// </summary>
/// <param name="scopeFactory">The DI scope factory used to resolve scoped repositories.</param>
/// <param name="timeProvider">The time provider controlling periodic execution ticks.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class ExpiredTokensCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiredTokensCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(TokenConstants.CleanupIntervalHours);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogCleanupServiceStarting(logger);

        using var timer = new PeriodicTimer(_cleanupInterval, timeProvider);

        try
        {
            await CleanupTokensAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupTokensAsync(stoppingToken);
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

    private async Task CleanupTokensAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogCleanupStarted(logger);

            await using var scope = scopeFactory.CreateAsyncScope();
            var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IUserRefreshTokenRepository>();
            var actionTokenRepository = scope.ServiceProvider.GetRequiredService<IUserActionTokenRepository>();

            await refreshTokenRepository.RemoveExpiredTokensAsync(cancellationToken);
            await actionTokenRepository.RemoveExpiredTokensAsync(cancellationToken);

            LogCleanupSuccessfullyFinished(logger);
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

    [LoggerMessage(EventId = LogEventIds.CleanupServiceStarting, Level = LogLevel.Information, Message = "Expired tokens cleanup background service is starting.")]
    private static partial void LogCleanupServiceStarting(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupServiceStopping, Level = LogLevel.Information, Message = "Expired tokens cleanup background service is stopping.")]
    private static partial void LogCleanupServiceStopping(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupServiceFatalError, Level = LogLevel.Critical, Message = "A fatal error occurred in the expired tokens cleanup service.")]
    private static partial void LogCleanupServiceFatalError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = LogEventIds.CleanupStarted, Level = LogLevel.Information, Message = "Starting expired tokens cleanup...")]
    private static partial void LogCleanupStarted(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupSuccessfullyFinished, Level = LogLevel.Information, Message = "Successfully finished expired tokens cleanup.")]
    private static partial void LogCleanupSuccessfullyFinished(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.CleanupFailed, Level = LogLevel.Error, Message = "Failed to clean up expired tokens from the database.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
