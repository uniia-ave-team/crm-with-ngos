using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Queries;

/// <summary>
/// Represents a query to retrieve detailed project information by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the project to retrieve.</param>
public record GetProjectByIdQuery(Guid Id) : IRequest<ProjectDto>, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => Id;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
