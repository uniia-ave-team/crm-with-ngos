using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Commands;

/// <summary>
/// Represents a command to update the status, topic, or end date of a task (e.g., board drag-and-drop or status transition).
/// </summary>
/// <param name="Status">The new execution status of the task.</param>
/// <param name="TopicId">An optional board topic/section ID to move the task into.</param>
/// <param name="EndDate">An optional explicit UTC completion/end date for the task.</param>
public record UpdateTaskStatusCommand(
    [property: JsonRequired] ProjectTaskStatus Status,
    Guid? TopicId = null,
    DateTime? EndDate = null) : IRequest, ITransactionalCommand, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the parent project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the task to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid TaskId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
