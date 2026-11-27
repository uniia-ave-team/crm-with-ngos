using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Represents a query to retrieve all members assigned to a project.
/// </summary>
/// <param name="ProjectId">The unique identifier of the project.</param>
public record GetProjectMembersQuery(Guid ProjectId)
    : IQuery<List<ProjectMemberDto>>, IProjectScoped, ICurrentUserAware
{
    /// <inheritdoc />
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
