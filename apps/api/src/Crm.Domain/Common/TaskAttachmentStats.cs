namespace Crm.Domain.Common;

/// <summary>
/// Represents aggregate file attachment statistics for a single task.
/// </summary>
/// <param name="Count">The total number of attachments currently stored on the task.</param>
/// <param name="TotalBytes">The combined size in bytes of all attachments currently stored on the task.</param>
public sealed record TaskAttachmentStats(int Count, long TotalBytes)
{
    /// <summary>
    /// Gets an empty <see cref="TaskAttachmentStats"/> instance representing a task with no attachments.
    /// </summary>
    public static TaskAttachmentStats Empty { get; } = new(0, 0);
}
