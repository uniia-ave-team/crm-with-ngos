using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Represents a query to retrieve detailed project information by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the project to retrieve.</param>
public record GetProjectByIdQuery(Guid Id) : IQuery<ProjectDto>, IProjectScoped, ICurrentUserAware
{
    /// <inheritdoc />
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => Id;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
