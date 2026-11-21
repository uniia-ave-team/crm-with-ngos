using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="DeleteTaskAttachmentCommand"/> to delete a task attachment record and remove its file from storage.
/// </summary>
/// <param name="attachmentRepository">The repository for retrieving and deleting task attachment records.</param>
/// <param name="fileTracker">The transaction file tracker used to schedule physical files for deletion upon commit.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class DeleteTaskAttachmentCommandHandler(
    ITaskAttachmentRepository attachmentRepository,
    IFileTransactionTracker fileTracker,
    ILogger<DeleteTaskAttachmentCommandHandler> logger) : IRequestHandler<DeleteTaskAttachmentCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTaskAttachmentCommand request, CancellationToken cancellationToken)
    {
        LogDeletingTaskAttachment(logger, request.AttachmentId, request.TaskId);

        var attachment = await attachmentRepository.GetByTaskAndIdAsync(request.TaskId, request.AttachmentId, cancellationToken);
        fileTracker.RegisterFileForDeletion(attachment.StoredFileName, FileStorageConstants.TaskAttachmentsFolder);
        await attachmentRepository.DeleteAsync(attachment.Id, cancellationToken);

        LogTaskAttachmentDeletedSuccessfully(logger, request.AttachmentId, request.TaskId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTaskAttachment, Level = LogLevel.Information, Message = "Deleting attachment ID: {AttachmentId} from task ID: {TaskId}")]
    private static partial void LogDeletingTaskAttachment(ILogger logger, Guid attachmentId, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskAttachmentDeletedSuccessfully, Level = LogLevel.Information, Message = "Attachment ID: {AttachmentId} deleted from task ID: {TaskId}")]
    private static partial void LogTaskAttachmentDeletedSuccessfully(ILogger logger, Guid attachmentId, Guid taskId);
}
