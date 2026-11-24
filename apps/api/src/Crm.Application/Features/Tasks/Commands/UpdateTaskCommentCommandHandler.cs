using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="UpdateTaskCommentCommand"/> to edit a task comment.
/// </summary>
/// <param name="commentRepository">The repository for retrieving and updating task comments.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class UpdateTaskCommentCommandHandler(
    ITaskCommentRepository commentRepository,
    TimeProvider timeProvider,
    ILogger<UpdateTaskCommentCommandHandler> logger) : ICommandHandler<UpdateTaskCommentCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTaskCommentCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingTaskComment(logger, command.CommentId, command.TaskId);

        var comment = await commentRepository.GetByTaskAndIdForUpdateAsync(command.TaskId, command.CommentId, cancellationToken);

        if (comment.UserId != command.CurrentUserId)
        {
            throw new UnauthorizedAccessException(TaskErrorMessages.CommentUpdateForbidden);
        }

        comment.Content = command.Content.Trim();
        comment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        LogTaskCommentUpdatedSuccessfully(logger, command.CommentId, command.TaskId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTaskComment, Level = LogLevel.Information, Message = "Updating comment ID: {CommentId} on task ID: {TaskId}")]
    private static partial void LogUpdatingTaskComment(ILogger logger, Guid commentId, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentUpdatedSuccessfully, Level = LogLevel.Information, Message = "Comment ID: {CommentId} updated on task ID: {TaskId}")]
    private static partial void LogTaskCommentUpdatedSuccessfully(ILogger logger, Guid commentId, Guid taskId);
}
