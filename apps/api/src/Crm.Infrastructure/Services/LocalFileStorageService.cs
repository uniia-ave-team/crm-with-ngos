using System.Collections.Concurrent;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IFileStorageService"/> to manage physical files locally on the disk,
/// enforcing installation-wide storage quotas and atomic writes without leaving partial files on disk.
/// </summary>
/// <param name="options">The configuration options containing the UploadDirectory and storage quota.</param>
/// <param name="imageValidator">The validator verifying uploaded image content.</param>
public class LocalFileStorageService(
    IOptions<FileStorageOptions> options,
    IImageValidator imageValidator) : IFileStorageService
{
    private const int DefaultBufferSize = 4096;
    private const string TempFileExtension = ".tmp";

    private static readonly ConcurrentDictionary<string, StorageQuotaGuard> QuotaGuards =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<string> SaveFileAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default)
    {
        ValidateStream(contentStream);
        ValidateFileName(originalFileName);

        return SaveValidatedAsync(contentStream, originalFileName, folderName, cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> SaveImageAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default)
    {
        ValidateStream(contentStream);
        ValidateFileName(originalFileName);

        using var validationResult = imageValidator.Validate(contentStream, originalFileName);

        return !validationResult.IsValid
            ? throw new ArgumentException("The file content does not match a supported image format.", nameof(originalFileName))
            : SaveValidatedAsync(validationResult.ResultStream ?? contentStream, originalFileName, folderName, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream> GetFileAsync(string fileName, string folderName, CancellationToken cancellationToken = default)
    {
        if (ShouldIgnoreFile(fileName))
        {
            throw new ArgumentException("The specified file identifier is an external URL or invalid, not a local stored file.", nameof(fileName));
        }

        string filePath = GetSecureFilePath(folderName, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{fileName}' was not found in storage folder '{folderName}'.", filePath);
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

        string filePath = GetSecureFilePath(folderName, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private async Task<string> SaveValidatedAsync(Stream stream, string name, string folder, CancellationToken cancellationToken)
    {
        string rootDirectory = GetNormalizedUploadDirectory();
        string uniqueName = GenerateUniqueFileName(name);
        string directoryPath = GetTargetDirectory(folder, rootDirectory);
        long maxTotalBytes = options.Value.MaxTotalBytes;
        long incomingBytes = stream.Length - stream.Position;

        StorageQuotaGuard guard = QuotaGuards.GetOrAdd(rootDirectory, _ => new StorageQuotaGuard());
        bool reserved = false;

        try
        {
            guard.Reserve(rootDirectory, incomingBytes, maxTotalBytes);
            reserved = true;

            EnsureDirectoryExists(directoryPath);

            string filePath = Path.Combine(directoryPath, uniqueName);
            await SaveAtomicallyToDiskAsync(stream, filePath, incomingBytes, maxTotalBytes, cancellationToken);

            return uniqueName;
        }
        finally
        {
            if (reserved)
            {
                guard.Release(incomingBytes);
            }
        }
    }

    private string GetSecureFilePath(string folderName, string fileName)
    {
        string baseDirectory = GetTargetDirectory(folderName, GetNormalizedUploadDirectory());
        string fullBaseDirectory = Path.GetFullPath(baseDirectory);

        if (!fullBaseDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            fullBaseDirectory += Path.DirectorySeparatorChar;
        }

        string fullFilePath = Path.GetFullPath(Path.Combine(fullBaseDirectory, fileName));

        return !fullFilePath.StartsWith(fullBaseDirectory, StringComparison.OrdinalIgnoreCase)
            ? throw new UnauthorizedAccessException("Path traversal attempt detected in file name.")
            : fullFilePath;
    }

    private string GetNormalizedUploadDirectory()
    {
        string uploadDir = options.Value.UploadDirectory;

        if (!Path.IsPathRooted(uploadDir))
        {
            uploadDir = Path.Combine(AppContext.BaseDirectory, uploadDir);
        }

        string normalizedUploadDir = Path.GetFullPath(uploadDir);

        if (!normalizedUploadDir.EndsWith(Path.DirectorySeparatorChar))
        {
            normalizedUploadDir += Path.DirectorySeparatorChar;
        }

        return normalizedUploadDir;
    }

    private static string GetTargetDirectory(string folderName, string normalizedUploadDir)
    {
        string targetDir = Path.GetFullPath(Path.Combine(normalizedUploadDir, folderName));

        return !targetDir.StartsWith(normalizedUploadDir, StringComparison.OrdinalIgnoreCase)
            ? throw new UnauthorizedAccessException("Path traversal attempt detected in folder name.")
            : targetDir;
    }

    private static void ValidateStream(Stream contentStream)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        if (!contentStream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable to validate its signature.", nameof(contentStream));
        }

        if (contentStream.Length == 0)
        {
            throw new ArgumentException("The file stream cannot be empty.", nameof(contentStream));
        }

        contentStream.Position = 0;
    }

    private static void ValidateFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("The original file name must be provided.", nameof(originalFileName));
        }
    }

    private static string GenerateUniqueFileName(string originalFileName)
    {
        string extension = Path.GetExtension(originalFileName);
        return $"{Guid.NewGuid()}{extension}";
    }

    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static async Task SaveAtomicallyToDiskAsync(
        Stream sourceStream,
        string filePath,
        long expectedMaxBytes,
        long maxTotalBytes,
        CancellationToken cancellationToken)
    {
        string tempFilePath = $"{filePath}.{Guid.NewGuid():N}{TempFileExtension}";
        byte[] buffer = new byte[DefaultBufferSize];
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
                while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
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
        }
        catch
        {
            TryDeleteFile(tempFilePath);
            throw;
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

    private static long CalculateDirectorySize(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return 0;
        }

        long totalBytes = 0;
        foreach (string filePath in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            if (filePath.EndsWith(TempFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                totalBytes += new FileInfo(filePath).Length;
            }
            catch (FileNotFoundException)
            {
                // Ignore files deleted concurrently during directory traversal.
            }
            catch (IOException)
            {
                // Ignore transient I/O errors on concurrently modified files.
            }
        }

        return totalBytes;
    }

    private static bool ShouldIgnoreFile(string fileName) =>
        string.IsNullOrWhiteSpace(fileName) ||
        fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private sealed class StorageQuotaGuard
    {
        private readonly Lock _syncRoot = new();
        private long _inFlightBytes;

        public void Reserve(string rootDirectory, long incomingBytes, long maxTotalBytes)
        {
            lock (_syncRoot)
            {
                long currentBytes = CalculateDirectorySize(rootDirectory);
                long reservedBytes = Interlocked.Read(ref _inFlightBytes);

                if (incomingBytes > maxTotalBytes || currentBytes + reservedBytes > maxTotalBytes - incomingBytes)
                {
                    throw new StorageQuotaExceededException(
                        $"File storage quota of {maxTotalBytes} bytes exceeded. Delete unused files or contact your administrator.");
                }

                Interlocked.Add(ref _inFlightBytes, incomingBytes);
            }
        }

        public void Release(long bytes)
            => Interlocked.Add(ref _inFlightBytes, -bytes);
    }
}
