using SkiaSharp;

namespace Crm.Infrastructure.Services.Storage.FileValidators;

/// <summary>
/// Validates raster image files (JPEG, PNG, WebP, GIF, BMP, ICO) to ensure correct encoding and dimensions using SkiaSharp.
/// </summary>
public sealed class RasterImageValidator : IFormatValidator
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".ico",
    };

    /// <inheritdoc />
    public bool CanHandle(string extension)
        => !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);

    /// <inheritdoc />
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
