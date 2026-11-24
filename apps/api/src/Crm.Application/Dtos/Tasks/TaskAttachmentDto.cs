namespace Crm.Application.Dtos.Tasks;

/// <summary>
/// Represents metadata and download URL for a file attached to a task.
/// </summary>
/// <param name="Id">The unique identifier of the attachment.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
/// <param name="UploadedBy">The unique identifier of the user who uploaded the attachment, if still available.</param>
/// <param name="OriginalFileName">The original file name supplied during upload.</param>
/// <param name="ContentType">The MIME content type of the attachment.</param>
/// <param name="FileSize">The file size in bytes.</param>
/// <param name="Url">The resolved download URL or storage key for retrieving the attachment stream.</param>
/// <param name="CreatedAt">The UTC timestamp when the attachment was uploaded.</param>
public record TaskAttachmentDto(
    Guid Id,
    Guid TaskId,
    Guid? UploadedBy,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    string? Url,
    DateTime CreatedAt);
