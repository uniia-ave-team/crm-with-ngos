using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Commands;

/// <summary>
/// Represents a command to upload and attach a file to a task.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
/// <param name="ContentStream">The readable stream containing the uploaded file bytes.</param>
/// <param name="OriginalFileName">The original file name supplied by the client.</param>
/// <param name="ContentType">The MIME content type of the uploaded file.</param>
/// <param name="FileSize">The size of the uploaded file in bytes.</param>
public record UploadTaskAttachmentCommand(
    Guid ProjectId,
    Guid TaskId,
    Stream ContentStream,
    string OriginalFileName,
    string ContentType,
    long FileSize) : IRequest<TaskAttachmentDto>, ITransactionalCommand, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
