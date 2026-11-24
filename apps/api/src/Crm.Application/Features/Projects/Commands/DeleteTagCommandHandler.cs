using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="DeleteTagCommand"/> to delete a project-scoped tag.
/// </summary>
/// <param name="tagRepository">The repository for deleting project-scoped tag entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class DeleteTagCommandHandler(
    ITagRepository tagRepository,
    ILogger<DeleteTagCommandHandler> logger) : ICommandHandler<DeleteTagCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTagCommand command, CancellationToken cancellationToken)
    {
        LogDeletingTag(logger, command.TagId, command.ProjectId);

        await tagRepository.EnsureExistsInProjectAsync(command.ProjectId, command.TagId, cancellationToken);
        await tagRepository.DeleteAsync(command.TagId, cancellationToken);

        LogTagDeletedSuccessfully(logger, command.TagId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTag, Level = LogLevel.Information, Message = "Deleting tag ID: {TagId} from project ID: {ProjectId}")]
    private static partial void LogDeletingTag(ILogger logger, Guid tagId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TagDeletedSuccessfully, Level = LogLevel.Information, Message = "Tag ID: {TagId} deleted from project ID: {ProjectId}")]
    private static partial void LogTagDeletedSuccessfully(ILogger logger, Guid tagId, Guid projectId);
}
