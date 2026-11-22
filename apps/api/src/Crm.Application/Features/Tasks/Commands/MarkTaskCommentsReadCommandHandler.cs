using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="MarkTaskCommentsReadCommand"/> to clear the authenticated user's unread comment state for the task.
/// </summary>
/// <param name="commentRepository">The repository for deleting task comment read-state markers.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class MarkTaskCommentsReadCommandHandler(
    ITaskCommentRepository commentRepository,
    ILogger<MarkTaskCommentsReadCommandHandler> logger) : ICommandHandler<MarkTaskCommentsReadCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(MarkTaskCommentsReadCommand command, CancellationToken cancellationToken)
    {
        LogMarkingTaskCommentsRead(logger, command.TaskId, command.CurrentUserId);

        await commentRepository.MarkAsReadAsync(command.TaskId, command.CurrentUserId, cancellationToken);

        LogTaskCommentsMarkedRead(logger, command.TaskId, command.CurrentUserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.MarkingTaskCommentsRead, Level = LogLevel.Information, Message = "Marking comments as read on task ID: {TaskId} for user ID: {UserId}")]
    private static partial void LogMarkingTaskCommentsRead(ILogger logger, Guid taskId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentsMarkedRead, Level = LogLevel.Information, Message = "Comments marked as read on task ID: {TaskId} for user ID: {UserId}")]
    private static partial void LogTaskCommentsMarkedRead(ILogger logger, Guid taskId, Guid userId);
}
