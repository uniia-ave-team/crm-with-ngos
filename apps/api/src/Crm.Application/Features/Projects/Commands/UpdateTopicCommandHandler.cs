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
/// Handles the <see cref="UpdateTopicCommand"/> to update a board column/section's title and display order.
/// </summary>
/// <param name="topicRepository">The repository for retrieving and updating board topic entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class UpdateTopicCommandHandler(
    ITopicRepository topicRepository,
    ILogger<UpdateTopicCommandHandler> logger) : IRequestHandler<UpdateTopicCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTopicCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingTopic(logger, request.TopicId, request.ProjectId);

        var topic = await topicRepository.GetByProjectAndIdForUpdateAsync(request.ProjectId, request.TopicId, cancellationToken);

        string trimmedTitle = request.Title.Trim();
        if (await topicRepository.ExistsWithTitleAsync(request.ProjectId, trimmedTitle, request.TopicId, cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Topic), trimmedTitle);
        }

        if (await topicRepository.ExistsWithOrderAsync(request.ProjectId, request.Order, request.TopicId, cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Topic), request.Order);
        }

        request.Adapt(topic);

        LogTopicUpdatedSuccessfully(logger, request.TopicId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTopic, Level = LogLevel.Information, Message = "Updating topic ID: {TopicId} in project ID: {ProjectId}")]
    private static partial void LogUpdatingTopic(ILogger logger, Guid topicId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TopicUpdatedSuccessfully, Level = LogLevel.Information, Message = "Topic ID: {TopicId} updated in project ID: {ProjectId}")]
    private static partial void LogTopicUpdatedSuccessfully(ILogger logger, Guid topicId, Guid projectId);
}
