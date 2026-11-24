using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="DeleteTaskCommand"/> to soft-delete a task. The task's attachments are deleted
/// for good: their rows now and their files once the transaction commits. Storage quota is measured on
/// disk, so keeping the files would mean that deleting tasks never frees any space.
/// </summary>
/// <param name="taskRepository">The repository for deleting task entities.</param>
/// <param name="attachmentRepository">The repository for deleting the task's attachments.</param>
/// <param name="fileTracker">The transaction file tracker used to delete the attachment files upon commit.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class DeleteTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    ITaskAttachmentRepository attachmentRepository,
    IFileTransactionTracker fileTracker,
    ILogger<DeleteTaskCommandHandler> logger) : ICommandHandler<DeleteTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTaskCommand command, CancellationToken cancellationToken)
    {
        LogDeletingTask(logger, command.TaskId, command.ProjectId);

        await taskRepository.LockForUpdateAsync(command.TaskId, cancellationToken);

        var storedFileNames = await attachmentRepository.DeleteByTaskIdAsync(command.TaskId, cancellationToken);
        foreach (string storedFileName in storedFileNames)
        {
            fileTracker.RegisterFileForDeletion(storedFileName, FileStorageConstants.TaskAttachmentsFolder);
        }

        await taskRepository.DeleteAsync(command.TaskId, cancellationToken);

        LogTaskDeletedSuccessfully(logger, command.TaskId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTask, Level = LogLevel.Information, Message = "Deleting task ID: {TaskId} from project ID: {ProjectId}")]
    private static partial void LogDeletingTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskDeletedSuccessfully, Level = LogLevel.Information, Message = "Task ID: {TaskId} deleted from project ID: {ProjectId}")]
    private static partial void LogTaskDeletedSuccessfully(ILogger logger, Guid taskId, Guid projectId);
}
