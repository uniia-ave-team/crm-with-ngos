using Crm.Application.Dtos.Common;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IImageValidator"/> using SkiaSharp for raster formats and Costasdev.SafeSvg for SVG files.
/// </summary>
public class ImageValidator(IEnumerable<IFormatValidator> validators) : IImageValidator
{
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
