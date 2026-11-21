using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Commands;

/// <summary>
/// Represents a command to update an existing comment on a task.
/// </summary>
/// <param name="Content">The updated text content of the comment.</param>
public record UpdateTaskCommentCommand(
    string Content) : IRequest, ITransactionalCommand, ITaskScoped
{
    /// <summary>
    /// Gets the unique identifier of the parent project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the task, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid TaskId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the comment to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid CommentId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
