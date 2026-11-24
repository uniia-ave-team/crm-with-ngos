using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Represents a command to delete a project and all its associated tasks, topics, tags, and member assignments.
/// </summary>
/// <param name="Id">The unique identifier of the project to delete.</param>
/// <param name="CurrentUserId">The unique identifier of the currently authenticated user performing the deletion.</param>
public record DeleteProjectCommand(
    Guid Id,
    Guid CurrentUserId) : ICommand, ITransactionalCommand, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => Id;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Manage;
}
