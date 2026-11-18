using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Decorator around <see cref="IFileStorageService"/> that skips physical disk writes and deletions
/// while Demo Mode is active, preserving all stream, extension, path-traversal, and image signature checks.
/// </summary>
/// <param name="inner">The underlying <see cref="IFileStorageService"/> implementation.</param>
/// <param name="fileStorageOptions">Configuration options providing the base upload directory.</param>
/// <param name="imageValidator">The image signature validator used for image uploads.</param>
/// <param name="demoModeState">The service providing current Demo Mode state.</param>
public sealed class DemoFileStorageService(
    IFileStorageService inner,
    IOptions<FileStorageOptions> fileStorageOptions,
    IImageValidator imageValidator,
    IDemoModeState demoModeState) : IFileStorageService
{
    /// <inheritdoc />
    public Task<string> SaveFileAsync(
        Stream contentStream,
        string originalFileName,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        if (!demoModeState.IsActive)
        {
            return inner.SaveFileAsync(contentStream, originalFileName, folderName, cancellationToken);
        }

        ValidateUpload(contentStream, originalFileName, folderName);
        return Task.FromResult(GenerateUniqueFileName(originalFileName));
    }

    /// <inheritdoc />
    public Task<string> SaveImageAsync(
        Stream contentStream,
        string originalFileName,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        if (!demoModeState.IsActive)
        {
            return inner.SaveImageAsync(contentStream, originalFileName, folderName, cancellationToken);
        }

        ValidateUpload(contentStream, originalFileName, folderName);

        using var validationResult = imageValidator.Validate(contentStream, originalFileName);
        return !validationResult.IsValid
            ? throw new ArgumentException("The file content does not match a supported image format.", nameof(originalFileName))
            : Task.FromResult(GenerateUniqueFileName(originalFileName));
    }

    /// <inheritdoc />
    public Task<Stream> GetFileAsync(
        string fileName,
        string folderName,
        CancellationToken cancellationToken = default)
        => inner.GetFileAsync(fileName, folderName, cancellationToken);

    /// <inheritdoc />
    public Task DeleteFileAsync(
        string fileName,
        string folderName,
        CancellationToken cancellationToken = default)
        => demoModeState.IsActive
            ? Task.CompletedTask
            : inner.DeleteFileAsync(fileName, folderName, cancellationToken);

    private void ValidateUpload(Stream contentStream, string originalFileName, string folderName)
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

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("The original file name must be provided.", nameof(originalFileName));
        }

        if (string.IsNullOrWhiteSpace(folderName))
        {
            throw new ArgumentException("The target folder name must be provided.", nameof(folderName));
        }

        EnsureSafeTargetDirectory(folderName);
    }

    private void EnsureSafeTargetDirectory(string folderName)
    {
        string uploadDir = fileStorageOptions.Value.UploadDirectory;
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
        if (!targetDir.StartsWith(normalizedUploadDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Path traversal attempt detected in folder name.");
        }
    }

    private static string GenerateUniqueFileName(string originalFileName)
        => $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
}
