using System.Net.Mime;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="UploadTaskAttachmentCommand"/> to store an uploaded file and create a <see cref="TaskAttachment"/> record.
/// </summary>
/// <param name="taskRepository">The repository for locking the parent task during attachment quota checks.</param>
/// <param name="attachmentRepository">The repository for persisting task attachment entities and evaluating task quotas.</param>
/// <param name="fileStorageService">The file storage service for saving uploaded file streams to storage.</param>
/// <param name="fileTracker">The transaction file tracker used to roll back created files if the transaction fails.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving the attachment download URL.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class UploadTaskAttachmentCommandHandler(
    IProjectTaskRepository taskRepository,
    ITaskAttachmentRepository attachmentRepository,
    IFileStorageService fileStorageService,
    IFileTransactionTracker fileTracker,
    IFileUrlProvider fileUrlProvider,
    TimeProvider timeProvider,
    ILogger<UploadTaskAttachmentCommandHandler> logger) : ICommandHandler<UploadTaskAttachmentCommand, TaskAttachmentDto>
{
    /// <inheritdoc />
    public async ValueTask<TaskAttachmentDto> Handle(UploadTaskAttachmentCommand command, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogUploadingTaskAttachment(logger, command.OriginalFileName, command.TaskId);

        await taskRepository.LockForUpdateAsync(command.TaskId, cancellationToken);

        var stats = await attachmentRepository.GetTaskAttachmentStatsAsync(command.TaskId, cancellationToken);
        if (stats.Count >= TaskValidationConstants.MaxAttachmentsPerTask)
        {
            throw new TaskAttachmentLimitExceededException(TaskValidationConstants.MaxAttachmentsPerTask);
        }

        if (stats.TotalBytes + command.FileSize > TaskValidationConstants.MaxTotalAttachmentSizePerTask)
        {
            throw new StorageQuotaExceededException(
                $"Total attachment size for a task cannot exceed {TaskValidationConstants.MaxTotalAttachmentSizePerTask} bytes.");
        }

        string safeOriginalFileName = command.OriginalFileName.ExtractFileName();

        string storedFileName = await fileStorageService.SaveFileAsync(
            command.ContentStream,
            safeOriginalFileName,
            FileStorageConstants.TaskAttachmentsFolder,
            cancellationToken);

        fileTracker.RegisterCreatedFile(storedFileName, FileStorageConstants.TaskAttachmentsFolder);

        var attachment = new TaskAttachment
        {
            TaskId = command.TaskId,
            UploadedBy = command.CurrentUserId,
            OriginalFileName = safeOriginalFileName,
            StoredFileName = storedFileName,
            ContentType = NormalizeContentType(command.ContentType),
            FileSize = command.FileSize,
            CreatedAt = utcNow,
        };

        await attachmentRepository.CreateAsync(attachment, cancellationToken);

        LogTaskAttachmentUploadedSuccessfully(logger, attachment.Id, command.TaskId);

        return attachment.Adapt<TaskAttachmentDto>().EnrichUrl(command.ProjectId, fileUrlProvider);
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return MediaTypeNames.Application.Octet;
        }

        int semicolonIndex = contentType.IndexOf(';');
        string normalized = (semicolonIndex >= 0 ? contentType[..semicolonIndex] : contentType).Trim();
        return string.IsNullOrEmpty(normalized) ? MediaTypeNames.Application.Octet : normalized;
    }

    [LoggerMessage(EventId = LogEventIds.UploadingTaskAttachment, Level = LogLevel.Information, Message = "Uploading attachment '{FileName}' for task ID: {TaskId}")]
    private static partial void LogUploadingTaskAttachment(ILogger logger, string fileName, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskAttachmentUploadedSuccessfully, Level = LogLevel.Information, Message = "Attachment ID: {AttachmentId} uploaded for task ID: {TaskId}")]
    private static partial void LogTaskAttachmentUploadedSuccessfully(ILogger logger, Guid attachmentId, Guid taskId);
}
