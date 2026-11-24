using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="DeleteTopicCommand"/> to remove a board column/section from a project
/// and compact the remaining topics' display order values to stay contiguous starting from 0.
/// </summary>
/// <param name="projectRepository">The repository used to lock the parent project against concurrent order mutations.</param>
/// <param name="topicRepository">The repository for checking task assignments and deleting board topics.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class DeleteTopicCommandHandler(
    IProjectRepository projectRepository,
    ITopicRepository topicRepository,
    ILogger<DeleteTopicCommandHandler> logger) : ICommandHandler<DeleteTopicCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTopicCommand command, CancellationToken cancellationToken)
    {
        LogDeletingTopic(logger, command.TopicId, command.ProjectId);

        await projectRepository.LockForUpdateAsync(command.ProjectId, cancellationToken);

        List<Topic> projectTopics = await topicRepository.GetByProjectIdForUpdateAsync(command.ProjectId, cancellationToken);
        Topic topicToDelete = projectTopics.FirstOrDefault(t => t.Id == command.TopicId)
            ?? throw new EntityNotFoundException<Topic>(command.TopicId);

        if (await topicRepository.HasTasksAsync(command.TopicId, cancellationToken))
        {
            throw new CannotDeleteTopicWithTasksException(command.TopicId);
        }

        await topicRepository.DeleteAsync(command.TopicId, cancellationToken);

        List<Topic> remainingTopics = [.. projectTopics.Where(t => t.Id != topicToDelete.Id)];
        remainingTopics.CompactSequential(static (item, order) => item.Order = order);
        await topicRepository.ResolveOrderCollisionsAsync(command.ProjectId, cancellationToken);

        LogTopicDeletedSuccessfully(logger, command.TopicId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTopic, Level = LogLevel.Information, Message = "Deleting topic ID: {TopicId} from project ID: {ProjectId}")]
    private static partial void LogDeletingTopic(ILogger logger, Guid topicId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TopicDeletedSuccessfully, Level = LogLevel.Information, Message = "Topic ID: {TopicId} deleted from project ID: {ProjectId}")]
    private static partial void LogTopicDeletedSuccessfully(ILogger logger, Guid topicId, Guid projectId);
}
