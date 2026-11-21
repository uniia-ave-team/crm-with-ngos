using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="UnassignUserFromTaskCommand"/> to remove a user assignment from a task.
/// </summary>
/// <param name="taskRepository">The repository for removing task user assignments and updating task timestamps.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class UnassignUserFromTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    TimeProvider timeProvider,
    ILogger<UnassignUserFromTaskCommandHandler> logger) : IRequestHandler<UnassignUserFromTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UnassignUserFromTaskCommand request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogUnassigningUserFromTask(logger, request.TaskId, request.ProjectId);

        if (await taskRepository.UnassignFromTaskAsync(request.TaskId, request.UserId, cancellationToken))
        {
            await taskRepository.UpdateUpdatedAtAsync(request.TaskId, utcNow, cancellationToken);
        }

        LogUserUnassignedFromTask(logger, request.TaskId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UnassigningUserFromTask, Level = LogLevel.Information, Message = "Unassigning user from task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogUnassigningUserFromTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.UserUnassignedFromTask, Level = LogLevel.Information, Message = "User unassigned from task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogUserUnassignedFromTask(ILogger logger, Guid taskId, Guid projectId);
}
