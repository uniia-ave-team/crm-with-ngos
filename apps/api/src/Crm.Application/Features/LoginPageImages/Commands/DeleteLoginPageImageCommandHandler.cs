using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Commands;

/// <summary>
/// Handles the <see cref="DeleteLoginPageImageCommand"/> to remove an existing login page image from the system,
/// including its physical file from storage and its corresponding record from the database.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="fileTransactionTracker">The service responsible for tracking and managing file deletion transactions to ensure consistency between storage and database records.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the login page image deletion process.</param>
public sealed partial class DeleteLoginPageImageCommandHandler(
    ILoginPageImageRepository repository,
    IFileTransactionTracker fileTransactionTracker,
    ILogger<DeleteLoginPageImageCommandHandler> logger) : ICommandHandler<DeleteLoginPageImageCommand>
{
    public async ValueTask<Unit> Handle(DeleteLoginPageImageCommand command, CancellationToken cancellationToken)
    {
        LogDeletingImage(logger, command.Id);

        await TryDeleteLoginPageImageFileAsync(command.Id, cancellationToken);

        await repository.DeleteAsync(command.Id, cancellationToken);

        LogImageDeletedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    /// <summary>
    /// Retrieves the stored file name of the login page image and registers it for deletion upon transaction commit.
    /// </summary>
    /// <param name="imageId">The unique identifier of the login page image.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    private async Task TryDeleteLoginPageImageFileAsync(Guid imageId, CancellationToken cancellationToken)
    {
        var fileName = await repository.GetUrlAsync(imageId, cancellationToken);
        fileTransactionTracker.RegisterFileForDeletion(fileName, FileStorageConstants.LoginPageImagesFolder);
    }

    [LoggerMessage(EventId = LogEventIds.DeletingLoginPageImage, Level = LogLevel.Information, Message = "Initiating deletion of login page image with ID: {Id}")]
    private static partial void LogDeletingImage(ILogger logger, Guid id);

    [LoggerMessage(EventId = LogEventIds.LoginPageImageDeletedSuccessfully, Level = LogLevel.Information, Message = "Login page image with ID: {Id} successfully deleted.")]
    private static partial void LogImageDeletedSuccessfully(ILogger logger, Guid id);
}
