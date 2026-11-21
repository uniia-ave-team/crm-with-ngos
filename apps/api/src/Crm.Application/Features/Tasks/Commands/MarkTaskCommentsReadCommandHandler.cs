using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="MarkTaskCommentsReadCommand"/> to clear the authenticated user's unread comment state for the task.
/// </summary>
/// <param name="commentRepository">The repository for deleting task comment read-state markers.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class MarkTaskCommentsReadCommandHandler(
    ITaskCommentRepository commentRepository,
    ICurrentUserService currentUserService,
    ILogger<MarkTaskCommentsReadCommandHandler> logger) : IRequestHandler<MarkTaskCommentsReadCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(MarkTaskCommentsReadCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();

        LogMarkingTaskCommentsRead(logger, request.TaskId, currentUserId);

        await commentRepository.MarkAsReadAsync(request.TaskId, currentUserId, cancellationToken);

        LogTaskCommentsMarkedRead(logger, request.TaskId, currentUserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.MarkingTaskCommentsRead, Level = LogLevel.Information, Message = "Marking comments as read on task ID: {TaskId} for user ID: {UserId}")]
    private static partial void LogMarkingTaskCommentsRead(ILogger logger, Guid taskId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentsMarkedRead, Level = LogLevel.Information, Message = "Comments marked as read on task ID: {TaskId} for user ID: {UserId}")]
    private static partial void LogTaskCommentsMarkedRead(ILogger logger, Guid taskId, Guid userId);
}
