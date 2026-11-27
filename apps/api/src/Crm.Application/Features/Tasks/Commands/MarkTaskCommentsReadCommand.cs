using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Represents a command to mark all comments on a task as read for the currently authenticated user.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
public record MarkTaskCommentsReadCommand(
    Guid ProjectId,
    Guid TaskId) : ICommand, ITransactionalCommand, ITaskScoped, ICurrentUserAware
{
    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
