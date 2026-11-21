using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Common;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Queries;

/// <summary>
/// Represents a query to download the file stream of a specific task attachment.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
/// <param name="AttachmentId">The unique identifier of the attachment to download.</param>
public record GetTaskAttachmentFileQuery(
    Guid ProjectId,
    Guid TaskId,
    Guid AttachmentId) : IRequest<FileDto>, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
