namespace Crm.Domain.Entities;

/// <summary>
/// Represents a file attached to a <see cref="ProjectTask"/> and stored in the local/Docker volume file storage.
/// </summary>
public class TaskAttachment : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the <see cref="ProjectTask"/>.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the <see cref="ProjectTask"/>.
    /// </summary>
    public ProjectTask Task { get; set; }

    /// <summary>
    /// Gets or sets the optional foreign key identifier of the <see cref="User"/> who uploaded the file.
    /// Set to <c>null</c> if the uploader is deleted.
    /// </summary>
    public Guid? UploadedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the uploader <see cref="User"/>.
    /// </summary>
    public User? Uploader { get; set; }

    /// <summary>
    /// Gets or sets the original file name provided during upload.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique stored file name on disk within the task attachments folder.
    /// </summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME content type of the attachment.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the attachment was uploaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
