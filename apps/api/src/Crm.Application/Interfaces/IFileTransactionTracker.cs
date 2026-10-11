namespace Crm.Application.Interfaces;

/// <summary>
/// Tracks file system operations within the scope of a single CQRS command.
/// Ensures file system consistency by deferring physical file deletions until
/// a database transaction successfully commits, and automatically cleaning up
/// newly created files if the transaction rolls back.
/// </summary>
public interface IFileTransactionTracker
{
    /// <summary>
    /// Registers a newly created file so it can be physically deleted if the database transaction fails.
    /// </summary>
    /// <param name="fileName">The name of the newly created file.</param>
    /// <param name="folderName">The storage folder where the file resides.</param>
    void RegisterCreatedFile(string fileName, string folderName);

    /// <summary>
    /// Registers an existing file for deletion. The actual physical deletion is deferred
    /// until the database transaction successfully commits.
    /// </summary>
    /// <param name="fileName">The name of the file to be deleted.</param>
    /// <param name="folderName">The storage folder where the file resides.</param>
    void RegisterFileForDeletion(string? fileName, string folderName);

    /// <summary>
    /// Executes physical deletion of all files marked for deletion.
    /// Should be called by the pipeline only after a successful database commit.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task CommitPendingDeletionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes physical deletion of newly created files to prevent orphaned files.
    /// Should be called by the pipeline when a database transaction rolls back.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task RollbackCreatedFilesAsync(CancellationToken cancellationToken = default);
}
