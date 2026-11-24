using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Represents a command to delete a comment from a task.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
/// <param name="CommentId">The unique identifier of the comment to delete.</param>
/// <param name="CurrentUserId">The unique identifier of the currently authenticated user requesting deletion.</param>
public record DeleteTaskCommentCommand(
    Guid ProjectId,
    Guid TaskId,
    Guid CommentId,
    Guid CurrentUserId) : ICommand, ITransactionalCommand, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
