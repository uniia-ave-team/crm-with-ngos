using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Commands;

/// <summary>
/// Represents a command to delete a board topic/section from a project.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TopicId">The unique identifier of the topic to delete.</param>
public record DeleteTopicCommand(
    Guid ProjectId,
    Guid TopicId) : IRequest, ITransactionalCommand, IProjectScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
