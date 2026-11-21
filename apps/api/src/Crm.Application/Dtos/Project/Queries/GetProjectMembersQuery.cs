using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Queries;

/// <summary>
/// Represents a query to retrieve all members assigned to a project.
/// </summary>
/// <param name="ProjectId">The unique identifier of the project.</param>
public record GetProjectMembersQuery(Guid ProjectId) : IRequest<List<ProjectMemberDto>>, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
