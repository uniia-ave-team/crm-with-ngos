using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Commands;

/// <summary>
/// Represents a command to update an existing task within a project.
/// </summary>
/// <param name="TopicId">The unique identifier of the required board topic/section for the task.</param>
/// <param name="Title">The updated display title of the task.</param>
/// <param name="Description">The updated optional Markdown-enabled description of the task.</param>
/// <param name="StartDate">The updated UTC start date/time of the task.</param>
/// <param name="Status">The updated execution status of the task (required).</param>
/// <param name="Priority">The updated priority level of the task (required).</param>
/// <param name="EndDate">The updated optional UTC end/completion date/time of the task.</param>
/// <param name="AssigneeIds">An optional list of user identifiers to synchronize as task assignees.</param>
/// <param name="TagIds">An optional list of project-scoped tag identifiers to synchronize as task tags.</param>
/// <param name="ExternalLinks">An optional list of external links to replace on the task when provided.</param>
/// <param name="Metadata">An optional JSON key-value metadata payload to replace on the task when provided.</param>
public record UpdateTaskCommand(
    [property: JsonRequired] Guid TopicId,
    string Title,
    IReadOnlyList<Guid> AssigneeIds,
    IReadOnlyList<Guid> TagIds,
    string? Description,
    [property: JsonRequired] DateTime StartDate,
    [property: JsonRequired] ProjectTaskStatus Status,
    [property: JsonRequired] TaskPriority Priority,
    DateTime? EndDate = null,
    IReadOnlyList<TaskExternalLinkDto>? ExternalLinks = null,
    JsonObject? Metadata = null) : IRequest, ITransactionalCommand, IProjectScoped
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
