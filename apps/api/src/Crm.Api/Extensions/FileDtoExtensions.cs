using System.Net.Mime;
using Crm.Api.Consts;
using Crm.Application.Dtos.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Crm.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="FileDto"/> and file stream responses.
/// </summary>
public static class FileDtoExtensions
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    /// <summary>
    /// Gets the corresponding MIME media type for the file, using <see cref="FileDto.ContentType"/> when specified
    /// or falling back to resolving the media type from its file extension.
    /// </summary>
    /// <param name="fileDto">The file data transfer object.</param>
    /// <returns>The content type string (e.g., "image/jpeg"), or application/octet-stream if not found.</returns>
    public static string GetContentType(this FileDto fileDto)
    {
        ArgumentNullException.ThrowIfNull(fileDto);

        if (!string.IsNullOrWhiteSpace(fileDto.ContentType))
        {
            return fileDto.ContentType;
        }

        if (!ContentTypeProvider.TryGetContentType(fileDto.FileName, out var contentType))
        {
            contentType = MediaTypeNames.Application.Octet;
        }

        return contentType;
    }

    /// <summary>
    /// Streams a <see cref="FileDto"/> with range processing enabled and the <c>X-Content-Type-Options: nosniff</c> header applied.
    /// </summary>
    /// <param name="controller">The controller producing the response.</param>
    /// <param name="fileDto">The file data transfer object containing the stream and file name.</param>
    /// <param name="asAttachment">When <c>true</c>, includes <see cref="FileDto.FileName"/> to trigger attachment download headers.</param>
    /// <returns>A <see cref="FileStreamResult"/> configured for the file stream.</returns>
    public static FileStreamResult ServeFile(
        this ControllerBase controller,
        FileDto fileDto,
        bool asAttachment = false)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(fileDto);

        controller.Response.Headers.XContentTypeOptions = SecurityHeaderValues.NoSniff;

        return asAttachment
            ? controller.File(fileDto.Stream, fileDto.GetContentType(), fileDto.FileName, enableRangeProcessing: true)
            : controller.File(fileDto.Stream, fileDto.GetContentType(), enableRangeProcessing: true);
    }
}
