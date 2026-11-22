using Crm.Application.Common.Consts;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="UpdateTaskStatusCommand"/> to update a task's status, topic, and completion timestamp.
/// </summary>
/// <param name="topicRepository">The repository for verifying topic existence in the project.</param>
/// <param name="taskRepository">The repository for retrieving and updating project task entities.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class UpdateTaskStatusCommandHandler(
    ITopicRepository topicRepository,
    IProjectTaskRepository taskRepository,
    TimeProvider timeProvider,
    ILogger<UpdateTaskStatusCommandHandler> logger) : ICommandHandler<UpdateTaskStatusCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTaskStatusCommand command, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogUpdatingTaskStatus(logger, command.TaskId, command.ProjectId);

        var task = await taskRepository.GetByProjectAndIdForUpdateAsync(command.ProjectId, command.TaskId, cancellationToken: cancellationToken);

        if (command.TopicId.HasValue)
        {
            await topicRepository.EnsureExistsInProjectAsync(command.ProjectId, command.TopicId.Value, cancellationToken);
            task.TopicId = command.TopicId.Value;
        }

        task.TransitionStatus(command.Status, command.EndDate, utcNow);

        LogTaskStatusUpdated(logger, task.Id, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTaskStatus, Level = LogLevel.Information, Message = "Updating status for task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogUpdatingTaskStatus(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskStatusUpdated, Level = LogLevel.Information, Message = "Status updated for task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogTaskStatusUpdated(ILogger logger, Guid taskId, Guid projectId);
}
