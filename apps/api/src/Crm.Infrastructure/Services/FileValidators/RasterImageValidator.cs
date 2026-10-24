using Crm.Application.Dtos.Common;
using Crm.Application.Interfaces;
using SkiaSharp;

namespace Crm.Infrastructure.Services.FileValidators;

/// <summary>
/// Validates raster image files (JPEG, PNG, WebP, GIF, BMP, ICO) to ensure correct encoding and dimensions using SkiaSharp.
/// </summary>
public class RasterImageValidator : IFormatValidator
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".ico",
    };

    /// <inheritdoc />
    /// <summary>
    /// Determines whether the specified extension corresponds to a supported raster image format.
    /// </summary>
    /// <param name="extension">The file extension to check (e.g., <c>".png"</c>, <c>".jpg"</c>).</param>
    /// <returns>
    /// <see langword="true"/> if the extension is a supported raster image format; otherwise, <see langword="false"/>.
    /// </returns>
    public bool CanHandle(string extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);
    }

    /// <inheritdoc />
    /// <summary>
    /// Validates that the provided image stream can be decoded, has valid dimensions, and matches the expected format for the given file extension.
    /// </summary>
    /// <param name="stream">The image stream to validate.</param>
    /// <param name="extension">The target file extension to match against the encoded format.</param>
    /// <returns>
    /// A <see cref="FileValidationResult"/> indicating whether the image passed validation.
    /// </returns>
    public FileValidationResult Validate(Stream stream, string extension)
    {
        using var managedStream = new SKManagedStream(stream, disposeManagedStream: false);
        using var codec = SKCodec.Create(managedStream);

        bool isValid = codec is not null
            && codec.Info.Width > 0
            && codec.Info.Height > 0
            && IsFormatMatchingExtension(codec.EncodedFormat, extension);

        return isValid ? new FileValidationResult(IsValid: true) : new FileValidationResult(IsValid: false);
    }

    /// <summary>
    /// Checks whether the encoded image format detected by SkiaSharp matches the provided file extension.
    /// </summary>
    /// <param name="format">The encoded image format extracted from the stream.</param>
    /// <param name="extension">The file extension to compare against.</param>
    /// <returns>
    /// <see langword="true"/> if the format matches the extension; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsFormatMatchingExtension(SKEncodedImageFormat format, string extension) =>
        (format, extension.ToLowerInvariant()) switch
        {
            (SKEncodedImageFormat.Jpeg, ".jpg" or ".jpeg") => true,
            (SKEncodedImageFormat.Png, ".png") => true,
            (SKEncodedImageFormat.Webp, ".webp") => true,
            (SKEncodedImageFormat.Gif, ".gif") => true,
            (SKEncodedImageFormat.Bmp, ".bmp") => true,
            (SKEncodedImageFormat.Ico, ".ico") => true,
            _ => false,
        };
}
