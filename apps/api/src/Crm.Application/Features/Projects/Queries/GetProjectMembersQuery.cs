using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Represents a query to retrieve all members assigned to a project.
/// </summary>
/// <param name="ProjectId">The unique identifier of the project.</param>
/// <param name="CurrentUserId">The unique identifier of the currently authenticated user.</param>
public record GetProjectMembersQuery(
    Guid ProjectId,
    Guid CurrentUserId) : IQuery<List<ProjectMemberDto>>, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
