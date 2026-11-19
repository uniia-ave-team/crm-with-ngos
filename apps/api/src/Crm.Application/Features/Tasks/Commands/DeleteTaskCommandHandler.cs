using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="DeleteTaskCommand"/> to delete a task and schedule its attachment files for deletion.
/// </summary>
/// <param name="taskRepository">The repository for deleting task entities.</param>
/// <param name="attachmentRepository">The repository for retrieving stored attachment file names on the task.</param>
/// <param name="fileTracker">The transaction file tracker used to schedule physical files for deletion upon commit.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class DeleteTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    ITaskAttachmentRepository attachmentRepository,
    IFileTransactionTracker fileTracker,
    ILogger<DeleteTaskCommandHandler> logger) : IRequestHandler<DeleteTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        LogDeletingTask(logger, request.TaskId, request.ProjectId);

        var storedFileNames = await attachmentRepository.GetStoredFileNamesByTaskIdAsync(request.TaskId, cancellationToken);
        foreach (string storedFileName in storedFileNames)
        {
            fileTracker.RegisterFileForDeletion(storedFileName, FileStorageConstants.TaskAttachmentsFolder);
        }

        await taskRepository.DeleteAsync(request.TaskId, cancellationToken);

        LogTaskDeletedSuccessfully(logger, request.TaskId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTask, Level = LogLevel.Information, Message = "Deleting task ID: {TaskId} from project ID: {ProjectId}")]
    private static partial void LogDeletingTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskDeletedSuccessfully, Level = LogLevel.Information, Message = "Task ID: {TaskId} deleted from project ID: {ProjectId}")]
    private static partial void LogTaskDeletedSuccessfully(ILogger logger, Guid taskId, Guid projectId);
}
