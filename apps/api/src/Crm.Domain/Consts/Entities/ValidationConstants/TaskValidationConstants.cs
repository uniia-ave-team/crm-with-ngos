using System.Collections.Frozen;

namespace Crm.Domain.Consts.Entities.ValidationConstants;

/// <summary>
/// Provides validation and configuration constants for project task, comment, external link, metadata, and attachment entities.
/// </summary>
public static class TaskValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a task title.
    /// </summary>
    public const int MaxTitleLength = 255;

    /// <summary>
    /// The maximum allowed length for a task description (supports Markdown content).
    /// </summary>
    public const int MaxDescriptionLength = 10000;

    /// <summary>
    /// The maximum allowed number of external links attached to a single task.
    /// </summary>
    public const int MaxExternalLinksCount = 20;

    /// <summary>
    /// The maximum allowed length for an external link title.
    /// </summary>
    public const int MaxExternalLinkTitleLength = 200;

    /// <summary>
    /// The maximum allowed length for an external link URL.
    /// </summary>
    public const int MaxExternalLinkUrlLength = 2048;

    /// <summary>
    /// The maximum allowed number of top-level properties in a task metadata JSON object.
    /// </summary>
    public const int MaxMetadataProperties = 50;

    /// <summary>
    /// The maximum allowed length for any metadata property key.
    /// </summary>
    public const int MaxMetadataKeyLength = 100;

    /// <summary>
    /// The maximum allowed nesting depth for a task metadata JSON object.
    /// </summary>
    public const int MaxMetadataDepth = 4;

    /// <summary>
    /// The maximum allowed serialized character length (in UTF-16 chars) for a task metadata JSON payload (16 KB).
    /// </summary>
    public const int MaxMetadataSerializedChars = 16 * 1024;

    /// <summary>
    /// The maximum allowed length for a task comment content.
    /// </summary>
    public const int MaxCommentContentLength = 5000;

    /// <summary>
    /// The maximum allowed length for an uploaded task attachment's original file name.
    /// </summary>
    public const int MaxAttachmentFileNameLength = 255;

    /// <summary>
    /// The maximum allowed length for a task attachment's stored file name.
    /// </summary>
    public const int MaxAttachmentStoredFileNameLength = 500;

    /// <summary>
    /// The maximum allowed length for a task attachment's MIME content type.
    /// </summary>
    public const int MaxAttachmentContentTypeLength = 150;

    /// <summary>
    /// The maximum allowed file size in bytes for an uploaded task attachment (10 MB).
    /// </summary>
    public const long MaxAttachmentFileSize = 10 * FileSizeUnits.Megabyte;

    /// <summary>
    /// Maps allow-listed task attachment file extensions (lowercase with leading dot) to their permitted MIME content types.
    /// Excludes executable, script, and active web content types such as HTML, SVG, JavaScript, and binaries.
    /// </summary>
    public static readonly FrozenDictionary<string, FrozenSet<string>> AllowedAttachmentExtensions =
        new (string Extension, string[] MimeTypes)[]
        {
        // Text & Data
        (".txt",  ["text/plain"]),
        (".md",   ["text/markdown", "text/plain"]),
        (".csv",  ["text/csv", "text/plain", "application/vnd.ms-excel"]),
        (".json", ["application/json", "text/plain"]),

        // Documents
        (".pdf",  ["application/pdf"]),
        (".rtf",  ["application/rtf", "text/rtf"]),
        (".doc",  ["application/msword"]),
        (".docx", ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"]),
        (".odt",  ["application/vnd.oasis.opendocument.text"]),

        // Spreadsheets & Presentations
        (".xls",  ["application/vnd.ms-excel"]),
        (".xlsx", ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]),
        (".ods",  ["application/vnd.oasis.opendocument.spreadsheet"]),
        (".ppt",  ["application/vnd.ms-powerpoint"]),
        (".pptx", ["application/vnd.openxmlformats-officedocument.presentationml.presentation"]),
        (".odp",  ["application/vnd.oasis.opendocument.presentation"]),

        // Images
        (".png",  ["image/png"]),
        (".jpg",  ["image/jpeg"]),
        (".jpeg", ["image/jpeg"]),
        (".gif",  ["image/gif"]),
        (".webp", ["image/webp"]),
        (".bmp",  ["image/bmp"]),

        // Archives
        (".zip",  ["application/zip", "application/x-zip-compressed"]),
        }
        .ToFrozenDictionary(
            x => x.Extension,
            x => x.MimeTypes.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
}
