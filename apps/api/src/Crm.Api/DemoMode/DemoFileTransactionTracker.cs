using Crm.Application.Interfaces;

namespace Crm.Api.DemoMode;

/// <summary>
/// Decorator around <see cref="IFileTransactionTracker"/> that prevents pending file deletions
/// from accumulating or committing to disk and rolls back any created files when inside a Demo Mode dry-run scope.
/// </summary>
/// <param name="inner">The underlying <see cref="IFileTransactionTracker"/> implementation.</param>
/// <param name="executionContext">The Demo Mode dry-run execution scope tracker.</param>
public sealed class DemoFileTransactionTracker(
    IFileTransactionTracker inner,
    IDemoExecutionContext executionContext) : IFileTransactionTracker
{
    /// <inheritdoc />
    public void RegisterCreatedFile(string fileName, string folderName)
        => inner.RegisterCreatedFile(fileName, folderName);

    /// <inheritdoc />
    public void RegisterFileForDeletion(string? fileName, string folderName)
    {
        if (executionContext.IsDryRunActive || executionContext.IsSessionAuthActive)
        {
            return;
        }

        inner.RegisterFileForDeletion(fileName, folderName);
    }

    /// <inheritdoc />
    public Task CommitPendingDeletionsAsync(CancellationToken cancellationToken = default)
        => executionContext.IsDryRunActive || executionContext.IsSessionAuthActive
            ? inner.RollbackCreatedFilesAsync(CancellationToken.None)
            : inner.CommitPendingDeletionsAsync(cancellationToken);

    /// <inheritdoc />
    public Task RollbackCreatedFilesAsync(CancellationToken cancellationToken = default)
        => inner.RollbackCreatedFilesAsync(cancellationToken);
}
