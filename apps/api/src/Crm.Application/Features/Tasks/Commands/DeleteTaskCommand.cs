using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Represents a command to delete a task from a project.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task to delete.</param>
public record DeleteTaskCommand(
    Guid ProjectId,
    Guid TaskId) : ICommand, ITransactionalCommand, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
