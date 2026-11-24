using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Represents a command to delete a file attachment from a task and remove its underlying file from storage.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
/// <param name="AttachmentId">The unique identifier of the attachment to delete.</param>
public record DeleteTaskAttachmentCommand(
    Guid ProjectId,
    Guid TaskId,
    Guid AttachmentId) : ICommand, ITransactionalCommand, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
