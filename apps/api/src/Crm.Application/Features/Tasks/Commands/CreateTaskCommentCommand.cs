using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Represents a command to post a new comment on a task.
/// </summary>
/// <param name="Content">The text content of the comment.</param>
public record CreateTaskCommentCommand(
    string Content) : ICommand<Guid>, ITransactionalCommand, ITaskScoped
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
    /// Gets the unique identifier of the currently authenticated user posting the comment, populated by the controller.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
