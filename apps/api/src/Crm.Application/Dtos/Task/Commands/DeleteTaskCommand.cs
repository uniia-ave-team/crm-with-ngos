using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Commands;

/// <summary>
/// Represents a command to delete a task from a project.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task to delete.</param>
public record DeleteTaskCommand(
    Guid ProjectId,
    Guid TaskId) : IRequest, ITransactionalCommand, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
