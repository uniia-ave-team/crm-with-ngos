using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Tasks;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Represents a query to retrieve a single task by project ID and task ID.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task to retrieve.</param>
public record GetTaskByIdQuery(
    Guid ProjectId,
    Guid TaskId) : IQuery<TaskDto>, IProjectScoped, ICurrentUserAware
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
