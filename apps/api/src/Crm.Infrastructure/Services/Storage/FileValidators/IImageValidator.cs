namespace Crm.Infrastructure.Services.Storage.FileValidators;

/// <summary>
/// Provides validation mechanisms to check file content integrity and security for image uploads.
/// </summary>
public interface IImageValidator
{
    /// <summary>
    /// Validates whether the stream content matches a supported and valid image format.
    /// </summary>
    /// <param name="stream">The content stream of the file.</param>
    /// <param name="fileName">The original file name containing the extension.</param>
    /// <returns>A <see cref="FileValidationResult"/> describing whether the image is valid.</returns>
    FileValidationResult Validate(Stream stream, string fileName);
}
