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
/// Handles the <see cref="UpdateTagCommand"/> to update a project-scoped tag's title.
/// </summary>
/// <param name="tagRepository">The repository for retrieving and updating project-scoped tag entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class UpdateTagCommandHandler(
    ITagRepository tagRepository,
    ILogger<UpdateTagCommandHandler> logger) : IRequestHandler<UpdateTagCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingTag(logger, request.TagId, request.ProjectId);

        var tag = await tagRepository.GetByProjectAndIdForUpdateAsync(request.ProjectId, request.TagId, cancellationToken);

        string trimmedTitle = request.Title.Trim();
        if (await tagRepository.ExistsWithTitleAsync(request.ProjectId, trimmedTitle, request.TagId, cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(Tag), trimmedTitle);
        }

        request.Adapt(tag);

        LogTagUpdatedSuccessfully(logger, request.TagId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTag, Level = LogLevel.Information, Message = "Updating tag ID: {TagId} in project ID: {ProjectId}")]
    private static partial void LogUpdatingTag(ILogger logger, Guid tagId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TagUpdatedSuccessfully, Level = LogLevel.Information, Message = "Tag ID: {TagId} updated in project ID: {ProjectId}")]
    private static partial void LogTagUpdatedSuccessfully(ILogger logger, Guid tagId, Guid projectId);
}
