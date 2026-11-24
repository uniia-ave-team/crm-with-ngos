using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="CreateTaskCommentCommand"/> to add a comment to a task.
/// </summary>
/// <param name="commentRepository">The repository for persisting task comment entities.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class CreateTaskCommentCommandHandler(
    ITaskCommentRepository commentRepository,
    TimeProvider timeProvider,
    ILogger<CreateTaskCommentCommandHandler> logger) : ICommandHandler<CreateTaskCommentCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateTaskCommentCommand command, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogCreatingTaskComment(logger, command.TaskId, command.CurrentUserId);

        var comment = command.Adapt<TaskComment>();
        comment.UserId = command.CurrentUserId;
        comment.CreatedAt = utcNow;
        comment.UpdatedAt = utcNow;

        await commentRepository.CreateAsync(comment, cancellationToken);
        await commentRepository.MarkUnreadForOtherUsersAsync(comment, command.CurrentUserId, cancellationToken);

        LogTaskCommentCreatedSuccessfully(logger, comment.Id, command.TaskId);

        return comment.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingTaskComment, Level = LogLevel.Information, Message = "Creating comment on task ID: {TaskId} by user ID: {UserId}")]
    private static partial void LogCreatingTaskComment(ILogger logger, Guid taskId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentCreatedSuccessfully, Level = LogLevel.Information, Message = "Comment ID: {CommentId} created on task ID: {TaskId}")]
    private static partial void LogTaskCommentCreatedSuccessfully(ILogger logger, Guid commentId, Guid taskId);
}
