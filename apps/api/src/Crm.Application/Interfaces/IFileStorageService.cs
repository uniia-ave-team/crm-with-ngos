using Crm.Domain.Consts;

namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a service for saving, retrieving, and managing uploaded files.
/// Abstracted from HTTP contexts to allow cross-platform and cloud-agnostic usage.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Asynchronously saves a file stream to the specified folder and returns its unique generated name.
    /// </summary>
    /// <param name="contentStream">The stream containing the file data.</param>
    /// <param name="originalFileName">The original name of the file, used to extract the extension safely.</param>
    /// <param name="folderName">The target folder name (e.g., from <see cref="FileStorageConstants"/>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A unique file name (e.g., GUID-based) under which the file was saved.</returns>
    Task<string> SaveFileAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously saves an image stream with signature validation to the specified folder and returns its unique generated name.
    /// </summary>
    /// <param name="contentStream">The stream containing the image data.</param>
    /// <param name="originalFileName">The original name of the image file.</param>
    /// <param name="folderName">The target folder name (e.g., from <see cref="FileStorageConstants"/>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A unique file name under which the validated image was saved.</returns>
    /// <exception cref="ArgumentException">Thrown if the file is not a valid image or has an unsupported extension.</exception>
    Task<string> SaveImageAsync(Stream contentStream, string originalFileName, string folderName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only stream for the specified file to serve it directly via API.
    /// </summary>
    /// <param name="fileName">The name of the file to retrieve or an external URL.</param>
    /// <param name="folderName">The folder where the file resides (e.g., from <see cref="FileStorageConstants"/>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Stream"/> of the file.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided file name is an external URL rather than a local file.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the physical file does not exist on disk.</exception>
    Task<Stream> GetFileAsync(string fileName, string folderName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes a file from the storage.
    /// </summary>
    /// <param name="fileName">The name of the file to delete.</param>
    /// <param name="folderName">The folder name where the file resides.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteFileAsync(string fileName, string folderName, CancellationToken cancellationToken = default);
}
