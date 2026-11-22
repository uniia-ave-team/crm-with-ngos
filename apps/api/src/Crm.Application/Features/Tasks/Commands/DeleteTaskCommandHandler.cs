using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="DeleteTaskCommand"/> to soft-delete a task while preserving
/// attachment files on disk so soft-deleted data remains recoverable.
/// </summary>
/// <param name="taskRepository">The repository for deleting task entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class DeleteTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    ILogger<DeleteTaskCommandHandler> logger) : ICommandHandler<DeleteTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTaskCommand command, CancellationToken cancellationToken)
    {
        LogDeletingTask(logger, command.TaskId, command.ProjectId);

        await taskRepository.LockForUpdateAsync(command.TaskId, cancellationToken);
        await taskRepository.DeleteAsync(command.TaskId, cancellationToken);

        LogTaskDeletedSuccessfully(logger, command.TaskId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTask, Level = LogLevel.Information, Message = "Deleting task ID: {TaskId} from project ID: {ProjectId}")]
    private static partial void LogDeletingTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskDeletedSuccessfully, Level = LogLevel.Information, Message = "Task ID: {TaskId} deleted from project ID: {ProjectId}")]
    private static partial void LogTaskDeletedSuccessfully(ILogger logger, Guid taskId, Guid projectId);
}
