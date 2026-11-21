namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a task reaches its maximum allowed number of file attachments.
/// </summary>
/// <param name="maxAttachments">The maximum permitted number of attachments per task.</param>
public class TaskAttachmentLimitExceededException(int maxAttachments)
    : DomainValidationException($"Task cannot have more than {maxAttachments} attachments.");
