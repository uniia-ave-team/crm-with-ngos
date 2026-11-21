using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="DeleteTopicCommand"/> to remove a board column/section from a project.
/// </summary>
/// <param name="topicRepository">The repository for checking task assignments and deleting board topics.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class DeleteTopicCommandHandler(
    ITopicRepository topicRepository,
    ILogger<DeleteTopicCommandHandler> logger) : IRequestHandler<DeleteTopicCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTopicCommand request, CancellationToken cancellationToken)
    {
        LogDeletingTopic(logger, request.TopicId, request.ProjectId);

        await topicRepository.EnsureExistsInProjectAsync(request.ProjectId, request.TopicId, cancellationToken);

        if (await topicRepository.HasTasksAsync(request.TopicId, cancellationToken))
        {
            throw new CannotDeleteTopicWithTasksException(request.TopicId);
        }

        await topicRepository.DeleteAsync(request.TopicId, cancellationToken);

        LogTopicDeletedSuccessfully(logger, request.TopicId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTopic, Level = LogLevel.Information, Message = "Deleting topic ID: {TopicId} from project ID: {ProjectId}")]
    private static partial void LogDeletingTopic(ILogger logger, Guid topicId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TopicDeletedSuccessfully, Level = LogLevel.Information, Message = "Topic ID: {TopicId} deleted from project ID: {ProjectId}")]
    private static partial void LogTopicDeletedSuccessfully(ILogger logger, Guid topicId, Guid projectId);
}
