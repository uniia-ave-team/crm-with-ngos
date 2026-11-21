using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="RemoveTagFromTaskCommand"/> to remove a tag association from a task.
/// </summary>
/// <param name="taskRepository">The repository for updating task timestamps.</param>
/// <param name="tagRepository">The repository for verifying tag scope and removing tags from tasks.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class RemoveTagFromTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    ITagRepository tagRepository,
    TimeProvider timeProvider,
    ILogger<RemoveTagFromTaskCommandHandler> logger) : IRequestHandler<RemoveTagFromTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(RemoveTagFromTaskCommand request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogRemovingTagFromTask(logger, request.TaskId, request.ProjectId);

        await tagRepository.EnsureExistsInProjectAsync(request.ProjectId, request.TagId, cancellationToken);

        if (await tagRepository.RemoveFromTaskAsync(request.TaskId, request.TagId, cancellationToken))
        {
            await taskRepository.UpdateUpdatedAtAsync(request.TaskId, utcNow, cancellationToken);
        }

        LogTagRemovedFromTask(logger, request.TaskId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.RemovingTagFromTask, Level = LogLevel.Information, Message = "Removing tag from task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogRemovingTagFromTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TagRemovedFromTask, Level = LogLevel.Information, Message = "Tag removed from task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogTagRemovedFromTask(ILogger logger, Guid taskId, Guid projectId);
}
