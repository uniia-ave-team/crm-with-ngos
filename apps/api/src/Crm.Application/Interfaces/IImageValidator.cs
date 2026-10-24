using Crm.Application.Dtos.Common;

namespace Crm.Application.Interfaces;

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
    /// <returns><c>true</c> if the image stream is valid; otherwise, <c>false</c>.</returns>
    FileValidationResult Validate(Stream stream, string fileName);
}
