using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="DeleteProjectCommand"/> to soft-delete a project while preserving
/// attachment files on disk so soft-deleted data remains recoverable.
/// </summary>
/// <param name="projectRepository">The repository for deleting projects.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class DeleteProjectCommandHandler(
    IProjectRepository projectRepository,
    ILogger<DeleteProjectCommandHandler> logger) : ICommandHandler<DeleteProjectCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteProjectCommand command, CancellationToken cancellationToken)
    {
        LogDeletingProject(logger, command.Id, command.CurrentUserId);

        await projectRepository.LockForUpdateAsync(command.Id, cancellationToken);
        await projectRepository.DeleteAsync(command.Id, cancellationToken);

        LogProjectDeletedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingProject, Level = LogLevel.Information, Message = "Deleting project ID: {ProjectId} by user ID: {UserId}")]
    private static partial void LogDeletingProject(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.ProjectDeletedSuccessfully, Level = LogLevel.Information, Message = "Project ID: {ProjectId} deleted successfully.")]
    private static partial void LogProjectDeletedSuccessfully(ILogger logger, Guid projectId);
}
