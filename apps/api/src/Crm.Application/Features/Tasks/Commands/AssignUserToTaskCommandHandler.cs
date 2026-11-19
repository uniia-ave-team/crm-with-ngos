using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
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
public partial class AssignUserToTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    IProjectAccessService projectAccessService,
    ITaskAssigneeValidator assigneeValidator,
    TimeProvider timeProvider,
    ILogger<AssignUserToTaskCommandHandler> logger) : IRequestHandler<AssignUserToTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(AssignUserToTaskCommand request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogAssigningUserToTask(logger, request.TaskId, request.ProjectId);

        var projectAccess = await projectAccessService.GetRequiredAsync(request.ProjectId, cancellationToken);
        await assigneeValidator.ValidateAsync([request.UserId], request.ProjectId, projectAccess, cancellationToken);

        if (await taskRepository.AssignToTaskAsync(request.TaskId, request.UserId, cancellationToken))
        {
            await taskRepository.UpdateUpdatedAtAsync(request.TaskId, utcNow, cancellationToken);
        }

        LogUserAssignedToTask(logger, request.TaskId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.AssigningUserToTask, Level = LogLevel.Information, Message = "Assigning user to task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogAssigningUserToTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.UserAssignedToTask, Level = LogLevel.Information, Message = "User assigned to task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogUserAssignedToTask(ILogger logger, Guid taskId, Guid projectId);
}
