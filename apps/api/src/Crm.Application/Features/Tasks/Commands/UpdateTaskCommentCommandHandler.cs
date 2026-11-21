using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="UpdateTaskCommentCommand"/> to edit a task comment.
/// </summary>
/// <param name="commentRepository">The repository for retrieving and updating task comments.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class UpdateTaskCommentCommandHandler(
    ITaskCommentRepository commentRepository,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    ILogger<UpdateTaskCommentCommandHandler> logger) : IRequestHandler<UpdateTaskCommentCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTaskCommentCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogUpdatingTaskComment(logger, request.CommentId, request.TaskId);

        var comment = await commentRepository.GetByTaskAndIdForUpdateAsync(request.TaskId, request.CommentId, cancellationToken);

        if (comment.UserId != currentUserId)
        {
            throw new UnauthorizedAccessException(TaskErrorMessages.CommentUpdateForbidden);
        }

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        LogTaskCommentUpdatedSuccessfully(logger, request.CommentId, request.TaskId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTaskComment, Level = LogLevel.Information, Message = "Updating comment ID: {CommentId} on task ID: {TaskId}")]
    private static partial void LogUpdatingTaskComment(ILogger logger, Guid commentId, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentUpdatedSuccessfully, Level = LogLevel.Information, Message = "Comment ID: {CommentId} updated on task ID: {TaskId}")]
    private static partial void LogTaskCommentUpdatedSuccessfully(ILogger logger, Guid commentId, Guid taskId);
}
