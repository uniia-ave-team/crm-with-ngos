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
/// Handles the <see cref="CreateTagCommand"/> to create a new project-scoped tag.
/// </summary>
/// <param name="tagRepository">The repository for persisting project-scoped tag entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class CreateTagCommandHandler(
    ITagRepository tagRepository,
    ILogger<CreateTagCommandHandler> logger) : IRequestHandler<CreateTagCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        LogCreatingTag(logger, request.ProjectId);

        string trimmedTitle = request.Title.Trim();
        if (await tagRepository.ExistsWithTitleAsync(request.ProjectId, trimmedTitle, cancellationToken: cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Tag), trimmedTitle);
        }

        var tag = request.Adapt<Tag>();
        await tagRepository.CreateAsync(tag, cancellationToken);

        LogTagCreatedSuccessfully(logger, tag.Id, request.ProjectId);

        return tag.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingTag, Level = LogLevel.Information, Message = "Creating tag in project ID: {ProjectId}")]
    private static partial void LogCreatingTag(ILogger logger, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TagCreatedSuccessfully, Level = LogLevel.Information, Message = "Tag ID: {TagId} created in project ID: {ProjectId}")]
    private static partial void LogTagCreatedSuccessfully(ILogger logger, Guid tagId, Guid projectId);
}
