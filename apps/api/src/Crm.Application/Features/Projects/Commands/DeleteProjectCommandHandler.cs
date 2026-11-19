using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="DeleteProjectCommand"/> to delete a project and schedule its task attachments for cleanup.
/// </summary>
/// <param name="projectRepository">The repository for deleting projects.</param>
/// <param name="attachmentRepository">The repository for retrieving stored attachment file names across the project.</param>
/// <param name="fileTracker">The transaction file tracker used to schedule physical files for deletion upon commit.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class DeleteProjectCommandHandler(
    IProjectRepository projectRepository,
    ITaskAttachmentRepository attachmentRepository,
    IFileTransactionTracker fileTracker,
    ICurrentUserService currentUserService,
    ILogger<DeleteProjectCommandHandler> logger) : IRequestHandler<DeleteProjectCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        LogDeletingProject(logger, request.Id, userId);

        var storedFileNames = await attachmentRepository.GetStoredFileNamesByProjectIdAsync(request.Id, cancellationToken);
        foreach (var storedFileName in storedFileNames)
        {
            fileTracker.RegisterFileForDeletion(storedFileName, FileStorageConstants.TaskAttachmentsFolder);
        }

        await projectRepository.DeleteAsync(request.Id, cancellationToken);

        LogProjectDeletedSuccessfully(logger, request.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingProject, Level = LogLevel.Information, Message = "Deleting project ID: {ProjectId} by user ID: {UserId}")]
    private static partial void LogDeletingProject(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.ProjectDeletedSuccessfully, Level = LogLevel.Information, Message = "Project ID: {ProjectId} deleted successfully.")]
    private static partial void LogProjectDeletedSuccessfully(ILogger logger, Guid projectId);
}
