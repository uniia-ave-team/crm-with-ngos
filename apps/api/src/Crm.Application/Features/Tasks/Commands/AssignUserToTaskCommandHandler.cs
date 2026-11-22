using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="AssignUserToTaskCommand"/> to assign a user to a task.
/// </summary>
/// <param name="taskRepository">The repository for managing task user assignments and updating task timestamps.</param>
/// <param name="projectAccessService">The scoped service resolving cached project access metadata.</param>
/// <param name="assigneeValidator">The validator verifying task assignee eligibility.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class AssignUserToTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    IProjectAccessService projectAccessService,
    ITaskAssigneeValidator assigneeValidator,
    TimeProvider timeProvider,
    ILogger<AssignUserToTaskCommandHandler> logger) : ICommandHandler<AssignUserToTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(AssignUserToTaskCommand command, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogAssigningUserToTask(logger, command.TaskId, command.ProjectId);

        var projectAccess = await projectAccessService.GetRequiredAsync(command.ProjectId, cancellationToken);
        await assigneeValidator.ValidateAsync([command.UserId], command.ProjectId, projectAccess, cancellationToken);

        if (await taskRepository.AssignToTaskAsync(command.TaskId, command.UserId, cancellationToken))
        {
            await taskRepository.UpdateUpdatedAtAsync(command.TaskId, utcNow, cancellationToken);
        }

        LogUserAssignedToTask(logger, command.TaskId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.AssigningUserToTask, Level = LogLevel.Information, Message = "Assigning user to task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogAssigningUserToTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.UserAssignedToTask, Level = LogLevel.Information, Message = "User assigned to task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogUserAssignedToTask(ILogger logger, Guid taskId, Guid projectId);
}
