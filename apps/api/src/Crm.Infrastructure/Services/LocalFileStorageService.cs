using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IFileStorageService"/> to manage physical files locally on the disk.
/// </summary>
/// <param name="options">The configuration options containing the UploadDirectory.</param>
public class LocalFileStorageService(
    IOptions<FileStorageOptions> options,
    IImageValidator imageValidator) : IFileStorageService
{
    private const int DefaultBufferSize = 4096;

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
        string uniqueName = GenerateUniqueFileName(name);
        string directoryPath = GetTargetDirectory(folder);

        EnsureDirectoryExists(directoryPath);

        string filePath = Path.Combine(directoryPath, uniqueName);
        await SaveToDiskAsync(stream, filePath, cancellationToken);

        return uniqueName;
    }

    private string GetSecureFilePath(string folderName, string fileName)
    {
        string baseDirectory = GetTargetDirectory(folderName);
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

    private string GetTargetDirectory(string folderName)
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

        string targetDir = Path.GetFullPath(Path.Combine(normalizedUploadDir, folderName));

        return !targetDir.StartsWith(normalizedUploadDir, StringComparison.OrdinalIgnoreCase)
            ? throw new UnauthorizedAccessException("Path traversal attempt detected in folder name.")
            : targetDir;
    }

    private static void ValidateStream(Stream contentStream)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        if (contentStream.Length == 0)
        {
            throw new ArgumentException("The file stream cannot be empty.", nameof(contentStream));
        }

        if (!contentStream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable to validate its signature.", nameof(contentStream));
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

    private static async Task SaveToDiskAsync(Stream sourceStream, string filePath, CancellationToken cancellationToken)
    {
        using var destinationStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            DefaultBufferSize,
            useAsync: true);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }

    private static bool ShouldIgnoreFile(string fileName) =>
        string.IsNullOrWhiteSpace(fileName) ||
        fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
