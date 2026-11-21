using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="DeleteTagCommand"/> to delete a project-scoped tag.
/// </summary>
/// <param name="tagRepository">The repository for deleting project-scoped tag entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class DeleteTagCommandHandler(
    ITagRepository tagRepository,
    ILogger<DeleteTagCommandHandler> logger) : IRequestHandler<DeleteTagCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        LogDeletingTag(logger, request.TagId, request.ProjectId);

        await tagRepository.EnsureExistsInProjectAsync(request.ProjectId, request.TagId, cancellationToken);
        await tagRepository.DeleteAsync(request.TagId, cancellationToken);

        LogTagDeletedSuccessfully(logger, request.TagId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTag, Level = LogLevel.Information, Message = "Deleting tag ID: {TagId} from project ID: {ProjectId}")]
    private static partial void LogDeletingTag(ILogger logger, Guid tagId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TagDeletedSuccessfully, Level = LogLevel.Information, Message = "Tag ID: {TagId} deleted from project ID: {ProjectId}")]
    private static partial void LogTagDeletedSuccessfully(ILogger logger, Guid tagId, Guid projectId);
}
