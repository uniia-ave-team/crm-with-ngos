using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implementation of the <see cref="IFileTransactionTracker"/> that coordinates
/// with <see cref="IFileStorageService"/> to manage physical file state during CQRS commands.
/// Ensures that physical file operations are synchronized with database transaction boundaries.
/// </summary>
/// <param name="fileStorageService">The service responsible for physical file I/O operations.</param>
/// <param name="logger">The logger used to record the tracking and execution of file operations.</param>
public partial class FileTransactionTracker(
    IFileStorageService fileStorageService,
    ILogger<FileTransactionTracker> logger) : IFileTransactionTracker
{
    private readonly List<(string FileName, string FolderName)> _createdFiles = [];
    private readonly List<(string FileName, string FolderName)> _filesToDelete = [];

    /// <inheritdoc />
    public void RegisterCreatedFile(string fileName, string folderName)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _createdFiles.Add((fileName, folderName));
            LogFileRegisteredForRollback(logger, fileName, folderName);
        }
    }

    /// <inheritdoc />
    public void RegisterFileForDeletion(string? fileName, string folderName)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _filesToDelete.Add((fileName, folderName));
            LogFileRegisteredForDeferredDeletion(logger, fileName, folderName);
        }
    }

    /// <inheritdoc />
    public async Task CommitPendingDeletionsAsync(CancellationToken cancellationToken = default)
    {
        if (_filesToDelete.Count == 0)
        {
            return;
        }

        LogCommittingFileDeletions(logger, _filesToDelete.Count);

        foreach (var (fileName, folderName) in _filesToDelete)
        {
            try
            {
                await fileStorageService.DeleteFileAsync(fileName, folderName, cancellationToken);
            }
            catch (Exception ex)
            {
                LogFailedToDeleteFile(logger, ex, fileName, folderName);
            }
        }

        _filesToDelete.Clear();
    }

    /// <inheritdoc />
    public async Task RollbackCreatedFilesAsync(CancellationToken cancellationToken = default)
    {
        if (_createdFiles.Count == 0)
        {
            return;
        }

        LogRollingBackCreatedFiles(logger, _createdFiles.Count);

        foreach (var (fileName, folderName) in _createdFiles)
        {
            try
            {
                await fileStorageService.DeleteFileAsync(fileName, folderName, cancellationToken);
            }
            catch (Exception ex)
            {
                LogFailedToCleanupOrphanedFile(logger, ex, fileName, folderName);
            }
        }

        _createdFiles.Clear();
    }

    [LoggerMessage(EventId = LogEventIds.FileRegisteredForRollback, Level = LogLevel.Debug, Message = "File '{FileName}' in '{FolderName}' registered for rollback cleanup.")]
    private static partial void LogFileRegisteredForRollback(ILogger logger, string fileName, string folderName);

    [LoggerMessage(EventId = LogEventIds.FileRegisteredForDeferredDeletion, Level = LogLevel.Debug, Message = "File '{FileName}' in '{FolderName}' registered for deferred deletion.")]
    private static partial void LogFileRegisteredForDeferredDeletion(ILogger logger, string fileName, string folderName);

    [LoggerMessage(EventId = LogEventIds.CommittingFileDeletions, Level = LogLevel.Information, Message = "Committing file transaction: Deleting {Count} old files.")]
    private static partial void LogCommittingFileDeletions(ILogger logger, int count);

    [LoggerMessage(EventId = LogEventIds.RollingBackCreatedFiles, Level = LogLevel.Warning, Message = "Rolling back file transaction: Deleting {Count} orphaned files due to command failure.")]
    private static partial void LogRollingBackCreatedFiles(ILogger logger, int count);

    [LoggerMessage(EventId = LogEventIds.FailedToDeleteFile, Level = LogLevel.Error, Message = "Failed to physically delete old file '{FileName}' from '{FolderName}' during commit phase.")]
    private static partial void LogFailedToDeleteFile(ILogger logger, Exception ex, string fileName, string folderName);

    [LoggerMessage(EventId = LogEventIds.FailedToCleanupOrphanedFile, Level = LogLevel.Error, Message = "Failed to physically cleanup orphaned file '{FileName}' from '{FolderName}' during rollback phase.")]
    private static partial void LogFailedToCleanupOrphanedFile(ILogger logger, Exception ex, string fileName, string folderName);
}
