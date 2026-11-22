using System.Buffers;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services.Storage.FileValidators;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services.Storage;

/// <summary>
/// Implements <see cref="IFileStorageService"/> to manage physical files locally on the disk,
/// enforcing installation-wide storage quotas via <see cref="ILocalStorageQuotaTracker"/> and atomic writes.
/// </summary>
/// <param name="options">The configuration options containing the UploadDirectory and storage quota.</param>
/// <param name="imageValidator">The validator verifying uploaded image content.</param>
/// <param name="quotaTracker">The quota tracker coordinating storage reservations across uploads.</param>
public sealed class LocalFileStorageService(
    IOptions<FileStorageOptions> options,
    IImageValidator imageValidator,
    ILocalStorageQuotaTracker quotaTracker) : IFileStorageService
{
    private const int DefaultBufferSize = 4096;
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <inheritdoc />
    public Task<string> SaveFileAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default)
    {
        ValidateStream(contentStream);
        ValidateFileName(originalFileName);

        return SaveValidatedAsync(contentStream, originalFileName, folderName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> SaveImageAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default)
    {
        ValidateStream(contentStream);
        ValidateFileName(originalFileName);

        using var validationResult = imageValidator.Validate(contentStream, originalFileName);

        return !validationResult.IsValid
            ? throw new DomainValidationException("The file content does not match a supported image format.")
            : await SaveValidatedAsync(validationResult.ResultStream ?? contentStream, originalFileName, folderName, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream> GetFileAsync(string fileName, string folderName, CancellationToken cancellationToken = default)
    {
        if (ShouldIgnoreFile(fileName))
        {
            throw new DomainValidationException("The specified file identifier is an external URL or invalid, not a local stored file.");
        }

        string filePath = GetSecureFilePath(folderName, fileName);

        if (!File.Exists(filePath))
        {
            throw new StorageFileNotFoundException(fileName, folderName);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, useAsync: true);

        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task DeleteFileAsync(string fileName, string folderName, CancellationToken cancellationToken = default)
    {
        if (ShouldIgnoreFile(fileName))
        {
            return Task.CompletedTask;
        }

        string rootDirectory = NormalizeUploadDirectory(options.Value.UploadDirectory);
        string filePath = GetSecureFilePath(folderName, fileName);

        if (!File.Exists(filePath))
        {
            return Task.CompletedTask;
        }

        // Atomically claim the file via rename so concurrent delete calls never double-subtract the same file's bytes.
        string deletingPath = $"{filePath}.{Guid.NewGuid():N}{LocalFileStorageConstants.TempFileExtension}";
        try
        {
            File.Move(filePath, deletingPath);
        }
        catch (FileNotFoundException)
        {
            return Task.CompletedTask;
        }
        catch (DirectoryNotFoundException)
        {
            return Task.CompletedTask;
        }

        long deletedBytes = TryGetFileLength(deletingPath);
        File.Delete(deletingPath);
        quotaTracker.OnFileDeleted(rootDirectory, deletedBytes);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves and normalizes a configured upload directory path to an absolute directory ending with a separator.
    /// </summary>
    /// <param name="uploadDirectory">The configured upload directory path (absolute or relative to base directory).</param>
    /// <returns>The normalized absolute directory path.</returns>
    internal static string NormalizeUploadDirectory(string uploadDirectory)
    {
        string resolved = Path.IsPathRooted(uploadDirectory)
            ? uploadDirectory
            : Path.Combine(AppContext.BaseDirectory, uploadDirectory);

        string normalized = Path.GetFullPath(resolved);

        return normalized.EndsWith(Path.DirectorySeparatorChar)
            ? normalized
            : normalized + Path.DirectorySeparatorChar;
    }

    private async Task<string> SaveValidatedAsync(Stream stream, string name, string folder, CancellationToken cancellationToken)
    {
        string rootDirectory = NormalizeUploadDirectory(options.Value.UploadDirectory);
        string uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(name)}";
        string directoryPath = GetTargetDirectory(folder, rootDirectory);
        long maxTotalBytes = options.Value.MaxTotalBytes;
        long incomingBytes = stream.Length - stream.Position;

        bool reserved = false;
        bool committed = false;

        try
        {
            quotaTracker.Reserve(rootDirectory, incomingBytes, maxTotalBytes);
            reserved = true;

            Directory.CreateDirectory(directoryPath);

            string filePath = Path.Combine(directoryPath, uniqueName);
            long writtenBytes = await SaveAtomicallyToDiskAsync(stream, filePath, incomingBytes, maxTotalBytes, cancellationToken);

            quotaTracker.Commit(rootDirectory, incomingBytes, writtenBytes);
            committed = true;

            return uniqueName;
        }
        finally
        {
            if (reserved && !committed)
            {
                quotaTracker.Release(rootDirectory, incomingBytes);
            }
        }
    }

    private string GetSecureFilePath(string folderName, string fileName)
    {
        string baseDirectory = GetTargetDirectory(folderName, NormalizeUploadDirectory(options.Value.UploadDirectory));
        string fullBaseDirectory = Path.GetFullPath(baseDirectory);

        if (!fullBaseDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            fullBaseDirectory += Path.DirectorySeparatorChar;
        }

        string fullFilePath = Path.GetFullPath(Path.Combine(fullBaseDirectory, fileName));

        return !fullFilePath.StartsWith(fullBaseDirectory, PathComparison)
            ? throw new UnauthorizedAccessException("Path traversal attempt detected in file name.")
            : fullFilePath;
    }

    private static string GetTargetDirectory(string folderName, string normalizedUploadDir)
    {
        string targetDir = Path.GetFullPath(Path.Combine(normalizedUploadDir, folderName));

        return !targetDir.StartsWith(normalizedUploadDir, PathComparison)
            ? throw new UnauthorizedAccessException("Path traversal attempt detected in folder name.")
            : targetDir;
    }

    private static void ValidateStream(Stream contentStream)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        if (!contentStream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable to determine its size and validate its contents.", nameof(contentStream));
        }

        if (contentStream.Length == 0)
        {
            throw new DomainValidationException("The file stream cannot be empty.");
        }

        contentStream.Position = 0;
    }

    private static void ValidateFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new DomainValidationException("The original file name must be provided.");
        }
    }

    private static async Task<long> SaveAtomicallyToDiskAsync(
        Stream sourceStream,
        string filePath,
        long expectedMaxBytes,
        long maxTotalBytes,
        CancellationToken cancellationToken)
    {
        string tempFilePath = $"{filePath}.{Guid.NewGuid():N}{LocalFileStorageConstants.TempFileExtension}";
        byte[] buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        long totalWritten = 0;

        try
        {
            await using (var destinationStream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                DefaultBufferSize,
                useAsync: true))
            {
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, DefaultBufferSize), cancellationToken)) > 0)
                {
                    totalWritten += bytesRead;
                    if (totalWritten > expectedMaxBytes)
                    {
                        throw new StorageQuotaExceededException(
                            $"File storage quota of {maxTotalBytes} bytes exceeded. Delete unused files or contact your administrator.");
                    }

                    await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                await destinationStream.FlushAsync(cancellationToken);
            }

            File.Move(tempFilePath, filePath);
            return totalWritten;
        }
        catch
        {
            TryDeleteFile(tempFilePath);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static long TryGetFileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of abandoned temporary upload file.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of abandoned temporary upload file.
        }
    }

    private static bool ShouldIgnoreFile(string fileName) =>
        string.IsNullOrWhiteSpace(fileName) ||
        fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
