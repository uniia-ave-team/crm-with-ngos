using Crm.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Stores the files a visitor uploads in the visitor's own folder and limits how much may be uploaded.
/// Files of the shared demo data stay readable from every sandbox but are never changed or deleted.
/// </summary>
/// <param name="inner">The regular file storage.</param>
/// <param name="sandboxAccessor">The accessor of the current sandbox.</param>
/// <param name="demoModeOptions">The demo mode options.</param>
/// <param name="logger">The logger.</param>
public sealed partial class DemoSandboxFileStorageService(
    IFileStorageService inner,
    IDemoSandboxAccessor sandboxAccessor,
    IOptions<DemoModeOptions> demoModeOptions,
    ILogger<DemoSandboxFileStorageService> logger) : IFileStorageService
{
    private readonly DemoSandboxOptions _options = demoModeOptions.Value.Sandbox;

    /// <inheritdoc />
    public Task<string> SaveFileAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default)
        => SaveAsync(contentStream, folderName, folder => inner.SaveFileAsync(contentStream, originalFileName, folder, cancellationToken));

    /// <inheritdoc />
    public Task<string> SaveImageAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default)
        => SaveAsync(contentStream, folderName, folder => inner.SaveImageAsync(contentStream, originalFileName, folder, cancellationToken));

    /// <inheritdoc />
    public async Task<Stream> GetFileAsync(string fileName, string folderName, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return await inner.GetFileAsync(fileName, folderName, cancellationToken);
        }

        try
        {
            return await inner.GetFileAsync(fileName, DemoSandboxPaths.GetSandboxFolder(sandbox.Id, folderName), cancellationToken);
        }
        catch (FileNotFoundException)
        {
            // Not uploaded in this sandbox: the file belongs to the shared demo data.
            return await inner.GetFileAsync(fileName, folderName, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task DeleteFileAsync(string fileName, string folderName, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is not null)
        {
            // Only the visitor's own copy is deleted; shared demo files stay for the other sandboxes.
            return inner.DeleteFileAsync(fileName, DemoSandboxPaths.GetSandboxFolder(sandbox.Id, folderName), cancellationToken);
        }

        return sandboxAccessor.IsHttpRequest
            ? Task.CompletedTask
            : inner.DeleteFileAsync(fileName, folderName, cancellationToken);
    }

    private async Task<string> SaveAsync(Stream contentStream, string folderName, Func<string, Task<string>> save)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            // Startup seeding may store files of the shared demo data; requests without a sandbox may not.
            return sandboxAccessor.IsHttpRequest
                ? throw new DemoModeException()
                : await save(folderName);
        }

        long size = contentStream.CanSeek ? contentStream.Length : 0;

        if (!sandbox.TryReserveUpload(size, _options.MaxUploadFiles, _options.MaxUploadBytes))
        {
            LogUploadQuotaExceeded(logger, sandbox.Id);

            throw new DemoModeException(
                DemoErrorCodes.QuotaExceeded,
                StatusCodes.Status403Forbidden,
                DemoErrorCodes.QuotaExceededMessage);
        }

        try
        {
            return await save(DemoSandboxPaths.GetSandboxFolder(sandbox.Id, folderName));
        }
        catch
        {
            sandbox.ReleaseUpload(size);
            throw;
        }
    }

    [LoggerMessage(EventId = DemoLogEventIds.SandboxUploadQuotaExceeded, Level = LogLevel.Warning, Message = "Demo sandbox {SandboxId} reached its upload quota.")]
    private static partial void LogUploadQuotaExceeded(ILogger logger, string sandboxId);
}
