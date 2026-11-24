namespace Crm.Infrastructure.Services.Storage.FileValidators;

/// <summary>
/// Represents the result of a file validation operation.
/// </summary>
/// <param name="IsValid">A value indicating whether the file passed validation.</param>
/// <param name="ResultStream">The new sanitized stream if the file content was modified; otherwise, <see langword="null"/>.</param>
public sealed record FileValidationResult(
    bool IsValid,
    Stream? ResultStream = null) : IDisposable
{
    private bool _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ResultStream?.Dispose();
        _disposed = true;
    }
}
