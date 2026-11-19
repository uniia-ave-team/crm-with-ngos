using System.Net.Mime;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="UploadTaskAttachmentCommand"/> to store an uploaded file in the Docker volume storage and create a <see cref="TaskAttachment"/> record.
/// </summary>
/// <param name="attachmentRepository">The repository for persisting task attachment entities.</param>
/// <param name="fileStorageService">The file storage service for saving uploaded file streams to disk.</param>
/// <param name="fileTracker">The transaction file tracker used to roll back created files if the transaction fails.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving the attachment download URL.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class UploadTaskAttachmentCommandHandler(
    ITaskAttachmentRepository attachmentRepository,
    IFileStorageService fileStorageService,
    IFileTransactionTracker fileTracker,
    IFileUrlProvider fileUrlProvider,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    ILogger<UploadTaskAttachmentCommandHandler> logger) : IRequestHandler<UploadTaskAttachmentCommand, TaskAttachmentDto>
{
    /// <inheritdoc />
    public async ValueTask<TaskAttachmentDto> Handle(UploadTaskAttachmentCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogUploadingTaskAttachment(logger, request.OriginalFileName, request.TaskId);

        string safeOriginalFileName = request.OriginalFileName.ExtractFileName();

        string storedFileName = await fileStorageService.SaveFileAsync(
            request.ContentStream,
            safeOriginalFileName,
            FileStorageConstants.TaskAttachmentsFolder,
            cancellationToken);

        fileTracker.RegisterCreatedFile(storedFileName, FileStorageConstants.TaskAttachmentsFolder);

        var attachment = new TaskAttachment
        {
            TaskId = request.TaskId,
            UploadedBy = currentUserId,
            OriginalFileName = safeOriginalFileName,
            StoredFileName = storedFileName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? MediaTypeNames.Application.Octet : request.ContentType,
            FileSize = request.FileSize,
            CreatedAt = utcNow,
        };

        await attachmentRepository.CreateAsync(attachment, cancellationToken);

        LogTaskAttachmentUploadedSuccessfully(logger, attachment.Id, request.TaskId);

        return attachment.Adapt<TaskAttachmentDto>().EnrichUrl(request.ProjectId, fileUrlProvider);
    }

    [LoggerMessage(EventId = LogEventIds.UploadingTaskAttachment, Level = LogLevel.Information, Message = "Uploading attachment '{FileName}' for task ID: {TaskId}")]
    private static partial void LogUploadingTaskAttachment(ILogger logger, string fileName, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskAttachmentUploadedSuccessfully, Level = LogLevel.Information, Message = "Attachment ID: {AttachmentId} uploaded for task ID: {TaskId}")]
    private static partial void LogTaskAttachmentUploadedSuccessfully(ILogger logger, Guid attachmentId, Guid taskId);
}
