using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Represents a command to delete a project-scoped tag.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TagId">The unique identifier of the tag to delete.</param>
public record DeleteTagCommand(
    Guid ProjectId,
    Guid TagId) : ICommand, ITransactionalCommand, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
