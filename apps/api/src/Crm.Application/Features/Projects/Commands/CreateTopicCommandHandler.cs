using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="CreateTopicCommand"/> to create a new board column/section in a project.
/// </summary>
/// <param name="topicRepository">The repository for persisting board topic entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class CreateTopicCommandHandler(
    ITopicRepository topicRepository,
    ILogger<CreateTopicCommandHandler> logger) : IRequestHandler<CreateTopicCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        LogCreatingTopic(logger, request.ProjectId);

        string trimmedTitle = request.Title.Trim();
        if (await topicRepository.ExistsWithTitleAsync(request.ProjectId, trimmedTitle, cancellationToken: cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Topic), trimmedTitle);
        }

        if (await topicRepository.ExistsWithOrderAsync(request.ProjectId, request.Order, cancellationToken: cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Topic), request.Order);
        }

        var topic = request.Adapt<Topic>();
        await topicRepository.CreateAsync(topic, cancellationToken);

        LogTopicCreatedSuccessfully(logger, topic.Id, request.ProjectId);

        return topic.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingTopic, Level = LogLevel.Information, Message = "Creating topic in project ID: {ProjectId}")]
    private static partial void LogCreatingTopic(ILogger logger, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TopicCreatedSuccessfully, Level = LogLevel.Information, Message = "Topic ID: {TopicId} created in project ID: {ProjectId}")]
    private static partial void LogTopicCreatedSuccessfully(ILogger logger, Guid topicId, Guid projectId);
}
