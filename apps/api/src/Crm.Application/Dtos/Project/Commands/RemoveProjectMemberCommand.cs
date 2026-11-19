using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Commands;

/// <summary>
/// Represents a command to remove a user from a project.
/// </summary>
/// <param name="ProjectId">The unique identifier of the project.</param>
/// <param name="UserId">The unique identifier of the user to remove from the project.</param>
public record RemoveProjectMemberCommand(
    Guid ProjectId,
    Guid UserId) : IRequest, ITransactionalCommand, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Manage;
}
