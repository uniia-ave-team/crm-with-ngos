using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Tasks;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Represents a command to upload and attach a file to a task.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
/// <param name="ContentStream">The readable stream containing the uploaded file bytes.</param>
/// <param name="OriginalFileName">The original file name supplied by the client.</param>
/// <param name="ContentType">The MIME content type of the uploaded file.</param>
/// <param name="FileSize">The size of the uploaded file in bytes.</param>
/// <param name="CurrentUserId">The unique identifier of the currently authenticated user uploading the attachment.</param>
public record UploadTaskAttachmentCommand(
    Guid ProjectId,
    Guid TaskId,
    Stream ContentStream,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    Guid CurrentUserId) : ICommand<TaskAttachmentDto>, ITransactionalCommand, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
