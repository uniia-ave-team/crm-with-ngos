using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="DeleteProjectCommand"/> to soft-delete a project. The project's tasks are deleted
/// for good (see <see cref="IProjectRepository"/>), and their attachment rows with them, so the attachment
/// files are deleted too once the transaction commits; otherwise nothing would ever reference them again.
/// </summary>
/// <param name="projectRepository">The repository for deleting projects.</param>
/// <param name="attachmentRepository">The repository for reading the stored file names of the project's attachments.</param>
/// <param name="fileTracker">The transaction file tracker used to delete the attachment files upon commit.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class DeleteProjectCommandHandler(
    IProjectRepository projectRepository,
    ITaskAttachmentRepository attachmentRepository,
    IFileTransactionTracker fileTracker,
    ILogger<DeleteProjectCommandHandler> logger) : ICommandHandler<DeleteProjectCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteProjectCommand command, CancellationToken cancellationToken)
    {
        LogDeletingProject(logger, command.Id, command.CurrentUserId);

        await projectRepository.LockForUpdateAsync(command.Id, cancellationToken);

        var storedFileNames = await attachmentRepository.GetStoredFileNamesByProjectIdAsync(command.Id, cancellationToken);
        foreach (string storedFileName in storedFileNames)
        {
            fileTracker.RegisterFileForDeletion(storedFileName, FileStorageConstants.TaskAttachmentsFolder);
        }

        await projectRepository.DeleteAsync(command.Id, cancellationToken);

        LogProjectDeletedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingProject, Level = LogLevel.Information, Message = "Deleting project ID: {ProjectId} by user ID: {UserId}")]
    private static partial void LogDeletingProject(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.ProjectDeletedSuccessfully, Level = LogLevel.Information, Message = "Project ID: {ProjectId} deleted successfully.")]
    private static partial void LogProjectDeletedSuccessfully(ILogger logger, Guid projectId);
}
