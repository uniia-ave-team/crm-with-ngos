using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="UpdateTopicCommand"/> to update a board column/section's title and display order
/// while preserving a contiguous 0-based <see cref="Topic.Order"/> sequence across the project.
/// </summary>
/// <param name="projectRepository">The repository used to lock the parent project against concurrent order mutations.</param>
/// <param name="topicRepository">The repository for retrieving and updating board topic entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class UpdateTopicCommandHandler(
    IProjectRepository projectRepository,
    ITopicRepository topicRepository,
    ILogger<UpdateTopicCommandHandler> logger) : ICommandHandler<UpdateTopicCommand>
{
    private const string NonSequentialTopicOrderMessage =
        "Topic display order values must be continuous and sequential starting from 0 without gaps.";

    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTopicCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingTopic(logger, command.TopicId, command.ProjectId);

        await projectRepository.LockForUpdateAsync(command.ProjectId, cancellationToken);

        List<Topic> projectTopics = await topicRepository.GetByProjectIdForUpdateAsync(command.ProjectId, cancellationToken);
        Topic topic = projectTopics.FirstOrDefault(t => t.Id == command.TopicId)
            ?? throw new EntityNotFoundException<Topic>(command.TopicId);

        string trimmedTitle = command.Title.Trim();
        if (await topicRepository.ExistsWithTitleAsync(command.ProjectId, trimmedTitle, command.TopicId, cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Topic), trimmedTitle);
        }

        projectTopics.ReorderSequential(
            topic,
            command.Order,
            static (item, order) => item.Order = order,
            onNonSequential: static () => new NonSequentialOrderException(NonSequentialTopicOrderMessage));

        topic.SetTitle(trimmedTitle);

        await topicRepository.ResolveOrderCollisionsAsync(command.ProjectId, cancellationToken);

        LogTopicUpdatedSuccessfully(logger, command.TopicId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTopic, Level = LogLevel.Information, Message = "Updating topic ID: {TopicId} in project ID: {ProjectId}")]
    private static partial void LogUpdatingTopic(ILogger logger, Guid topicId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TopicUpdatedSuccessfully, Level = LogLevel.Information, Message = "Topic ID: {TopicId} updated in project ID: {ProjectId}")]
    private static partial void LogTopicUpdatedSuccessfully(ILogger logger, Guid topicId, Guid projectId);
}
