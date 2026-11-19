using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Commands;

/// <summary>
/// Represents a command to create a new task within a project.
/// </summary>
/// <param name="TopicId">The unique identifier of the required board topic/section for the task.</param>
/// <param name="Title">The display title of the task.</param>
/// <param name="Description">The optional Markdown-enabled description of the task.</param>
/// <param name="StartDate">The UTC start date/time of the task.</param>
/// <param name="Status">The initial execution status of the task.</param>
/// <param name="Priority">The priority level of the task.</param>
/// <param name="EndDate">An optional UTC end/completion date/time of the task.</param>
/// <param name="AssigneeIds">An optional list of user identifiers to assign to the task.</param>
/// <param name="TagIds">An optional list of project-scoped tag identifiers to associate with the task.</param>
/// <param name="ExternalLinks">An optional list of external links attached to the task.</param>
/// <param name="Metadata">An optional flexible JSON key-value metadata payload attached to the task.</param>
public record CreateTaskCommand(
    Guid TopicId,
    string Title,
    IReadOnlyList<Guid> AssigneeIds,
    IReadOnlyList<Guid> TagIds,
    string? Description,
    DateTime StartDate,
    ProjectTaskStatus Status = ProjectTaskStatus.ToDo,
    TaskPriority Priority = TaskPriority.Medium,
    DateTime? EndDate = null,
    IReadOnlyList<TaskExternalLinkDto>? ExternalLinks = null,
    JsonObject? Metadata = null) : IRequest<Guid>, ITransactionalCommand, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the parent project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
