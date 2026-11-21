using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.Task.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Handles the <see cref="GetTaskAttachmentFileQuery"/> to retrieve the file stream of a task attachment from storage.
/// </summary>
/// <param name="attachmentRepository">The repository for retrieving task attachment metadata.</param>
/// <param name="fileStorageService">The file storage service for reading the stored file stream from disk.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetTaskAttachmentFileQueryHandler(
    ITaskAttachmentRepository attachmentRepository,
    IFileStorageService fileStorageService,
    ILogger<GetTaskAttachmentFileQueryHandler> logger) : IRequestHandler<GetTaskAttachmentFileQuery, FileDto>
{
    /// <inheritdoc />
    public async ValueTask<FileDto> Handle(GetTaskAttachmentFileQuery request, CancellationToken cancellationToken)
    {
        LogFetchingTaskAttachmentFile(logger, request.AttachmentId, request.TaskId);

        var attachment = await attachmentRepository.GetByTaskAndIdAsync(request.TaskId, request.AttachmentId, cancellationToken);

        var stream = await fileStorageService.GetFileAsync(
            attachment.StoredFileName,
            FileStorageConstants.TaskAttachmentsFolder,
            cancellationToken);

        LogTaskAttachmentFileFetched(logger, request.AttachmentId, request.TaskId);

        return new FileDto(stream, attachment.OriginalFileName);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTaskAttachmentFile, Level = LogLevel.Information, Message = "Fetching file stream for attachment ID: {AttachmentId} on task ID: {TaskId}")]
    private static partial void LogFetchingTaskAttachmentFile(ILogger logger, Guid attachmentId, Guid taskId);

    [LoggerMessage(EventId = LogEventIds.TaskAttachmentFileFetched, Level = LogLevel.Information, Message = "File stream retrieved for attachment ID: {AttachmentId} on task ID: {TaskId}")]
    private static partial void LogTaskAttachmentFileFetched(ILogger logger, Guid attachmentId, Guid taskId);
}
