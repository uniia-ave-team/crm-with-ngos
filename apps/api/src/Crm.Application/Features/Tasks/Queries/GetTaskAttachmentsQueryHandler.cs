using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Handles the <see cref="GetTaskAttachmentsQuery"/> to retrieve all file attachments for a task.
/// </summary>
/// <param name="attachmentRepository">The repository for querying task attachments.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving attachment download URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class GetTaskAttachmentsQueryHandler(
    ITaskAttachmentRepository attachmentRepository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetTaskAttachmentsQueryHandler> logger) : IQueryHandler<GetTaskAttachmentsQuery, List<TaskAttachmentDto>>
{
    /// <inheritdoc />
    public async ValueTask<List<TaskAttachmentDto>> Handle(GetTaskAttachmentsQuery query, CancellationToken cancellationToken)
    {
        LogFetchingTaskAttachments(logger, query.TaskId, query.ProjectId);

        var attachments = await attachmentRepository.GetByTaskIdAsync<TaskAttachmentDto>(query.TaskId, cancellationToken);
        var result = attachments.EnrichUrls(query.ProjectId, fileUrlProvider);

        LogTaskAttachmentsFetched(logger, result.Count, query.TaskId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTaskAttachments, Level = LogLevel.Information, Message = "Fetching attachments for task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogFetchingTaskAttachments(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskAttachmentsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} attachments for task ID: {TaskId}")]
    private static partial void LogTaskAttachmentsFetched(ILogger logger, int count, Guid taskId);
}
