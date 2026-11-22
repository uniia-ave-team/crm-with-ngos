using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Represents a query to retrieve detailed project information by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the project to retrieve.</param>
/// <param name="CurrentUserId">The unique identifier of the currently authenticated user.</param>
public record GetProjectByIdQuery(
    Guid Id,
    Guid CurrentUserId) : IQuery<ProjectDto>, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => Id;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
