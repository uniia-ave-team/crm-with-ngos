using System.Text.Json.Serialization;
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
public record DeleteTaskCommentCommand(
    Guid ProjectId,
    Guid TaskId,
    Guid CommentId) : ICommand, ITransactionalCommand, ITaskScoped, ICurrentUserAware
{
    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user requesting deletion, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
