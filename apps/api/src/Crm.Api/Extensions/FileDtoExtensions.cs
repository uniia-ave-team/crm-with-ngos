using System.Net.Mime;
using Crm.Application.Dtos.Common;
using Microsoft.AspNetCore.StaticFiles;

namespace Crm.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="FileDto"/>.
/// </summary>
public static class FileDtoExtensions
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    /// <summary>
    /// Gets the corresponding MIME media type for the file based on its extension.
    /// </summary>
    /// <param name="fileDto">The file data transfer object.</param>
    /// <returns>The content type string (e.g., "image/jpeg"), or application/octet-stream if not found.</returns>
    public static string GetContentType(this FileDto fileDto)
    {
        ArgumentNullException.ThrowIfNull(fileDto);

        if (!ContentTypeProvider.TryGetContentType(fileDto.FileName, out var contentType))
        {
            contentType = MediaTypeNames.Application.Octet;
        }

        return contentType;
    }
}
