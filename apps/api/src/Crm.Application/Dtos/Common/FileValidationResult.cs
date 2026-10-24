namespace Crm.Application.Dtos.Common;

/// <summary>
/// Represents the result of a file validation operation.
/// </summary>
/// <param name="IsValid">A value indicating whether the file passed validation.</param>
/// <param name="ResultStream">The new sanitized stream if the file content was modified; otherwise, <see langword="null"/>.</param>
public record FileValidationResult(
    bool IsValid,
    Stream? ResultStream = null) : IDisposable
{
    private bool _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="FileValidationResult"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            ResultStream?.Dispose();
        }

        _disposed = true;
    }
}
