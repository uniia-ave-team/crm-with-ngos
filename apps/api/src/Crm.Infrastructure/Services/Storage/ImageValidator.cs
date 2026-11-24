using Crm.Domain.Constants;
using Crm.Infrastructure.Services.Storage.FileValidators;

namespace Crm.Infrastructure.Services.Storage;

/// <summary>
/// Implements <see cref="IImageValidator"/> using SkiaSharp for raster formats and Costasdev.SafeSvg for SVG files.
/// </summary>
/// <param name="validators">The registered format-specific image validators.</param>
public sealed class ImageValidator(IEnumerable<IFormatValidator> validators) : IImageValidator
{
    /// <inheritdoc />
    public FileValidationResult Validate(Stream stream, string fileName)
    {
        if (stream is null || !stream.CanRead)
        {
            return new FileValidationResult(IsValid: false);
        }

        string extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(extension) || !FileStorageConstants.AllowedExtensions.Contains(extension))
        {
            return new FileValidationResult(IsValid: false);
        }

        var validator = validators.FirstOrDefault(v => v.CanHandle(extension));

        if (validator is null)
        {
            return new FileValidationResult(IsValid: false);
        }

        long originalPosition = 0;

        if (stream.CanSeek)
        {
            originalPosition = stream.Position;
            stream.Position = 0;
        }

        try
        {
            return validator.Validate(stream, extension);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }
}
