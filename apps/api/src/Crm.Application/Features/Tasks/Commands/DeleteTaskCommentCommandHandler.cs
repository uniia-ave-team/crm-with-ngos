using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="DeleteTaskCommentCommand"/> to delete a comment from a task.
/// </summary>
/// <param name="commentRepository">The repository for retrieving and deleting task comments.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="projectAccessService">The scoped service resolving cached project access metadata.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class DeleteTaskCommentCommandHandler(
    ITaskCommentRepository commentRepository,
    ICurrentUserService currentUserService,
    IProjectAccessService projectAccessService,
    ILogger<DeleteTaskCommentCommandHandler> logger) : IRequestHandler<DeleteTaskCommentCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTaskCommentCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogDeletingTaskComment(logger, request.CommentId, request.TaskId);

        var comment = await commentRepository.GetByTaskAndIdForUpdateAsync(request.TaskId, request.CommentId, cancellationToken);

        if (comment.UserId != currentUserId)
        {
            var projectAccess = await projectAccessService.GetRequiredAsync(request.ProjectId, cancellationToken);
            if (!projectAccess.CanManage)
            {
                throw new UnauthorizedAccessException(TaskErrorMessages.CommentDeleteForbidden);
            }
        }

        await commentRepository.DeleteAsync(comment.Id, cancellationToken);

        LogTaskCommentDeletedSuccessfully(logger, request.CommentId, request.TaskId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTaskComment, Level = LogLevel.Information, Message = "Deleting comment ID: {CommentId} from task ID: {TaskId}")]
    private static partial void LogDeletingTaskComment(ILogger logger, Guid commentId, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentDeletedSuccessfully, Level = LogLevel.Information, Message = "Comment ID: {CommentId} deleted from task ID: {TaskId}")]
    private static partial void LogTaskCommentDeletedSuccessfully(ILogger logger, Guid commentId, Guid taskId);
}
