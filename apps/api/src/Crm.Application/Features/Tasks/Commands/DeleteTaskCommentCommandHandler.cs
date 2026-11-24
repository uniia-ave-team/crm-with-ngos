using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="DeleteTaskCommentCommand"/> to delete a comment from a task.
/// </summary>
/// <param name="commentRepository">The repository for retrieving and deleting task comments.</param>
/// <param name="projectAccessService">The scoped service resolving cached project access metadata.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class DeleteTaskCommentCommandHandler(
    ITaskCommentRepository commentRepository,
    IProjectAccessService projectAccessService,
    ILogger<DeleteTaskCommentCommandHandler> logger) : ICommandHandler<DeleteTaskCommentCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTaskCommentCommand command, CancellationToken cancellationToken)
    {
        LogDeletingTaskComment(logger, command.CommentId, command.TaskId);

        var comment = await commentRepository.GetByTaskAndIdForUpdateAsync(command.TaskId, command.CommentId, cancellationToken);

        if (comment.UserId != command.CurrentUserId)
        {
            var projectAccess = await projectAccessService.GetRequiredAsync(command.ProjectId, cancellationToken);
            if (!projectAccess.CanManage)
            {
                throw new UnauthorizedAccessException(TaskErrorMessages.CommentDeleteForbidden);
            }
        }

        await commentRepository.DeleteAsync(comment.Id, cancellationToken);

        LogTaskCommentDeletedSuccessfully(logger, command.CommentId, command.TaskId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTaskComment, Level = LogLevel.Information, Message = "Deleting comment ID: {CommentId} from task ID: {TaskId}")]
    private static partial void LogDeletingTaskComment(ILogger logger, Guid commentId, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentDeletedSuccessfully, Level = LogLevel.Information, Message = "Comment ID: {CommentId} deleted from task ID: {TaskId}")]
    private static partial void LogTaskCommentDeletedSuccessfully(ILogger logger, Guid commentId, Guid taskId);
}
