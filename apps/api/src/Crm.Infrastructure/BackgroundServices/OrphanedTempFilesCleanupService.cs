using Crm.Application.Common.Consts;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically removes orphaned <c>*.tmp</c> upload files left on disk
/// after unexpected process crashes or interrupted disk operations.
/// </summary>
/// <param name="fileStorageOptions">The file storage configuration options.</param>
/// <param name="quotaTracker">The local storage quota tracker performing stale temporary file cleanup.</param>
/// <param name="timeProvider">The time provider controlling periodic execution ticks.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class OrphanedTempFilesCleanupService(
    IOptions<FileStorageOptions> fileStorageOptions,
    ILocalStorageQuotaTracker quotaTracker,
    TimeProvider timeProvider,
    ILogger<OrphanedTempFilesCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(LocalFileStorageConstants.TempFileCleanupIntervalHours);
    private static readonly TimeSpan StaleTempFileMaxAge = TimeSpan.FromHours(LocalFileStorageConstants.StaleTempFileMaxAgeHours);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarting(logger);

        using var timer = new PeriodicTimer(CleanupInterval, timeProvider);

        try
        {
            await CleanupStaleTempFilesAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupStaleTempFilesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogServiceStopping(logger);
        }
        catch (Exception ex)
        {
            LogServiceFatalError(logger, ex);
        }
    }

    private async Task CleanupStaleTempFilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            string rootDirectory = LocalFileStorageService.NormalizeUploadDirectory(fileStorageOptions.Value.UploadDirectory);
            int removedCount = await Task.Run(
                () => quotaTracker.CleanupStaleTempFiles(rootDirectory, StaleTempFileMaxAge, cancellationToken),
                cancellationToken);

            if (removedCount > 0)
            {
                LogCleanupCompleted(logger, removedCount);
            }
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

    [LoggerMessage(EventId = LogEventIds.TempFileCleanupServiceStarting, Level = LogLevel.Information, Message = "Orphaned temporary files cleanup background service is starting.")]
    private static partial void LogServiceStarting(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TempFileCleanupServiceStopping, Level = LogLevel.Information, Message = "Orphaned temporary files cleanup background service is stopping.")]
    private static partial void LogServiceStopping(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TempFileCleanupServiceFatalError, Level = LogLevel.Critical, Message = "A fatal error occurred in the orphaned temporary files cleanup service.")]
    private static partial void LogServiceFatalError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = LogEventIds.TempFileCleanupCompleted, Level = LogLevel.Information, Message = "Removed {RemovedCount} orphaned temporary upload file(s).")]
    private static partial void LogCleanupCompleted(ILogger logger, int removedCount);

    [LoggerMessage(EventId = LogEventIds.TempFileCleanupFailed, Level = LogLevel.Error, Message = "Failed to clean up orphaned temporary upload files.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
