using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="AddTagToTaskCommand"/> to associate a project-scoped tag with a task.
/// </summary>
/// <param name="taskRepository">The repository for updating task timestamps.</param>
/// <param name="tagRepository">The repository for verifying tag scope and attaching tags to tasks.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class AddTagToTaskCommandHandler(
    IProjectTaskRepository taskRepository,
    ITagRepository tagRepository,
    TimeProvider timeProvider,
    ILogger<AddTagToTaskCommandHandler> logger) : IRequestHandler<AddTagToTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(AddTagToTaskCommand request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogAddingTagToTask(logger, request.TaskId, request.ProjectId);

        await tagRepository.EnsureExistsInProjectAsync(request.ProjectId, request.TagId, cancellationToken);

        if (await tagRepository.AddToTaskAsync(request.TaskId, request.TagId, cancellationToken))
        {
            await taskRepository.UpdateUpdatedAtAsync(request.TaskId, utcNow, cancellationToken);
        }

        LogTagAddedToTask(logger, request.TaskId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.AddingTagToTask, Level = LogLevel.Information, Message = "Adding tag to task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogAddingTagToTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TagAddedToTask, Level = LogLevel.Information, Message = "Tag added to task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogTagAddedToTask(ILogger logger, Guid taskId, Guid projectId);
}
