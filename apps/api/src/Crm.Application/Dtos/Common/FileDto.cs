namespace Crm.Application.Dtos.Common;

/// <summary>
/// Represents a data transfer object containing a file stream and its name.
/// </summary>
/// <param name="Stream">The readable stream containing the file content.</param>
/// <param name="FileName">The stored name or original name of the file.</param>
public record FileDto(Stream Stream, string FileName) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
