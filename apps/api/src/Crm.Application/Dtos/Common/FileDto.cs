namespace Crm.Application.Dtos.Common;

/// <summary>
/// Represents a data transfer object containing a file stream, its file name, and optional MIME content type.
/// </summary>
/// <param name="Stream">The readable stream containing the file content.</param>
/// <param name="FileName">The stored name or original name of the file.</param>
/// <param name="ContentType">The optional explicit MIME media type of the file.</param>
public record FileDto(Stream Stream, string FileName, string? ContentType = null) : IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
