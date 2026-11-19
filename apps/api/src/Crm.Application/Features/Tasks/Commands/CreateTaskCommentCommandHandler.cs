using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Interfaces;
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
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class CreateTaskCommentCommandHandler(
    ITaskCommentRepository commentRepository,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    ILogger<CreateTaskCommentCommandHandler> logger) : IRequestHandler<CreateTaskCommentCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateTaskCommentCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogCreatingTaskComment(logger, request.TaskId, currentUserId);

        var comment = request.Adapt<TaskComment>();
        comment.UserId = currentUserId;
        comment.CreatedAt = utcNow;
        comment.UpdatedAt = utcNow;

        await commentRepository.CreateAsync(comment, cancellationToken);
        await commentRepository.MarkUnreadForOtherUsersAsync(comment, currentUserId, cancellationToken);

        LogTaskCommentCreatedSuccessfully(logger, comment.Id, request.TaskId);

        return comment.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingTaskComment, Level = LogLevel.Information, Message = "Creating comment on task ID: {TaskId} by user ID: {UserId}")]
    private static partial void LogCreatingTaskComment(ILogger logger, Guid taskId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentCreatedSuccessfully, Level = LogLevel.Information, Message = "Comment ID: {CommentId} created on task ID: {TaskId}")]
    private static partial void LogTaskCommentCreatedSuccessfully(ILogger logger, Guid commentId, Guid taskId);
}
