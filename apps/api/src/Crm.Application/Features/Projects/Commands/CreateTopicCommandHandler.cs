using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="CreateTopicCommand"/> to create a new board column/section in a project,
/// enforcing contiguous 0-based sequential <see cref="Topic.Order"/> values.
/// </summary>
/// <param name="projectRepository">The repository used to lock the parent project against concurrent order mutations.</param>
/// <param name="topicRepository">The repository for persisting board topic entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class CreateTopicCommandHandler(
    IProjectRepository projectRepository,
    ITopicRepository topicRepository,
    ILogger<CreateTopicCommandHandler> logger) : ICommandHandler<CreateTopicCommand, Guid>
{
    private const string NonSequentialTopicOrderMessage =
        "Topic display order values must be continuous and sequential starting from 0 without gaps.";

    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateTopicCommand command, CancellationToken cancellationToken)
    {
        LogCreatingTopic(logger, command.ProjectId);

        await projectRepository.LockForUpdateAsync(command.ProjectId, cancellationToken);

        string trimmedTitle = command.Title.Trim();
        if (await topicRepository.ExistsWithTitleAsync(command.ProjectId, trimmedTitle, cancellationToken: cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Topic), trimmedTitle);
        }

        List<byte> existingOrders = await topicRepository.GetOrdersByProjectIdAsync(command.ProjectId, cancellationToken);
        existingOrders.EnsureValidNextOrder(
            command.Order,
            onDuplicate: () => new EntityAlreadyExistsException(nameof(Topic), command.Order),
            onNonSequential: static () => new NonSequentialOrderException(NonSequentialTopicOrderMessage));

        var topic = command.Adapt<Topic>();
        await topicRepository.CreateAsync(topic, cancellationToken);

        LogTopicCreatedSuccessfully(logger, topic.Id, command.ProjectId);

        return topic.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingTopic, Level = LogLevel.Information, Message = "Creating topic in project ID: {ProjectId}")]
    private static partial void LogCreatingTopic(ILogger logger, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TopicCreatedSuccessfully, Level = LogLevel.Information, Message = "Topic ID: {TopicId} created in project ID: {ProjectId}")]
    private static partial void LogTopicCreatedSuccessfully(ILogger logger, Guid topicId, Guid projectId);
}
