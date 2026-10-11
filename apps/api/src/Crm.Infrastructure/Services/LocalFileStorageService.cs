using System.Text;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IFileStorageService"/> to manage physical files locally on the disk.
/// </summary>
/// <param name="options">The configuration options containing the UploadDirectory.</param>
public class LocalFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageService
{
    private const int DefaultBufferSize = 4096;

    private static readonly Dictionary<string, byte[][]> _imageSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpeg", [[0xFF, 0xD8, 0xFF]] },
        { ".jpg", [[0xFF, 0xD8, 0xFF]] },
        { ".png", [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]] },
        { ".gif", [[0x47, 0x49, 0x46, 0x38]] },
        { ".webp", [[0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]] },
        { ".bmp", [[0x42, 0x4D]] },
    };

    private static readonly Dictionary<string, int> _imageSignatureMaxLengths =
        _imageSignatures.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Max(s => s.Length),
            StringComparer.OrdinalIgnoreCase);

    private readonly FileStorageOptions _options = options.Value;

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
        ValidateImageSignature(contentStream, originalFileName);

        if (contentStream.CanSeek)
        {
            contentStream.Position = 0;
        }

        return SaveValidatedAsync(contentStream, originalFileName, folderName, cancellationToken);
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

        File.Delete(filePath);

        return Task.CompletedTask;
    }

    private async Task<string> SaveValidatedAsync(Stream s, string name, string folder, CancellationToken ct)
    {
        string uniqueName = GenerateUniqueFileName(name);
        string dir = GetTargetDirectory(folder);

        EnsureDirectoryExists(dir);

        await SaveToDiskAsync(s, Path.Combine(dir, uniqueName), ct);

        return uniqueName;
    }

    private string GetSecureFilePath(string folderName, string fileName)
    {
        string targetDirectory = GetTargetDirectory(folderName);
        string targetDirectoryFullPath = Path.GetFullPath(targetDirectory);

        string filePath = Path.GetFullPath(Path.Combine(targetDirectory, fileName));

        return !filePath.StartsWith(targetDirectoryFullPath, StringComparison.OrdinalIgnoreCase)
            ? throw new UnauthorizedAccessException("Path traversal attempt detected.")
            : filePath;
    }

    private static void ValidateStream(Stream contentStream)
    {
        if (contentStream is null || contentStream.Length == 0)
        {
            throw new ArgumentException("The file stream cannot be null or empty.", nameof(contentStream));
        }

        if (contentStream.CanSeek)
        {
            contentStream.Position = 0;
        }
    }

    private static void ValidateFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("The original file name must be provided.", nameof(originalFileName));
        }
    }

    private static void ValidateImageSignature(Stream stream, string fileName)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable to validate its signature.", nameof(stream));
        }

        string extension = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(extension) ||
            !_imageSignatures.TryGetValue(extension, out byte[][]? signatures) ||
            !_imageSignatureMaxLengths.TryGetValue(extension, out int maxSignatureLength))
        {
            throw new ArgumentException($"The file extension '{extension}' is not a supported image type.", nameof(fileName));
        }

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        byte[] headerBytes = reader.ReadBytes(maxSignatureLength);

        bool isValid = signatures.Any(sig => MatchesSignature(headerBytes, sig));

        if (!isValid)
        {
            throw new ArgumentException("The file signature does not match its extension.", nameof(fileName));
        }
    }

    private static bool MatchesSignature(byte[] header, byte[] signature)
    {
        for (int i = 0; i < signature.Length && i < header.Length; i++)
        {
            if (signature[i] != 0x00 && header[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string GenerateUniqueFileName(string originalFileName)
    {
        string extension = Path.GetExtension(originalFileName);
        return $"{Guid.NewGuid()}{extension}";
    }

    private string GetTargetDirectory(string folderName)
    {
        string uploadDir = _options.UploadDirectory;

        if (!Path.IsPathRooted(uploadDir))
        {
            uploadDir = Path.Combine(AppContext.BaseDirectory, uploadDir);
        }

        return Path.Combine(uploadDir, folderName);
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
        using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            DefaultBufferSize,
            useAsync: true);

        await sourceStream.CopyToAsync(fileStream, cancellationToken);
    }

    private static bool ShouldIgnoreFile(string fileName) =>
        string.IsNullOrWhiteSpace(fileName) ||
        fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
