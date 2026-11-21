using Crm.Application.Common.Consts;
using Crm.Domain.Consts;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically removes orphaned <c>*.tmp</c> upload files left on disk
/// after unexpected process crashes or interrupted disk operations.
/// </summary>
public partial class OrphanedTempFilesCleanupService(
    IOptions<FileStorageOptions> fileStorageOptions,
    ILogger<OrphanedTempFilesCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(FileStorageConstants.TempFileCleanupIntervalHours);
    private static readonly TimeSpan StaleTempFileMaxAge = TimeSpan.FromHours(FileStorageConstants.StaleTempFileMaxAgeHours);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarting(logger);

        using var timer = new PeriodicTimer(CleanupInterval);

        try
        {
            CleanupStaleTempFiles();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                CleanupStaleTempFiles();
            }
        }
        catch (OperationCanceledException)
        {
            LogServiceStopping(logger);
        }
        catch (Exception ex)
        {
            LogServiceFatalError(logger, ex);
        }
    }

    private void CleanupStaleTempFiles()
    {
        try
        {
            string rootDirectory = LocalFileStorageService.NormalizeUploadDirectory(fileStorageOptions.Value.UploadDirectory);
            int removedCount = LocalStorageQuotaTracker.CleanupStaleTempFiles(rootDirectory, StaleTempFileMaxAge);

            if (removedCount > 0)
            {
                LogCleanupCompleted(logger, removedCount);
            }
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
