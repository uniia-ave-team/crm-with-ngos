using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Commands;

/// <summary>
/// Handles the <see cref="DeleteLoginPageImageCommand"/> to remove an existing login page image from the system,
/// including its physical file from storage and its corresponding record from the database.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="fileTransactionTracker">The service responsible for tracking and managing file deletion transactions to ensure consistency between storage and database records.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the login page image deletion process.</param>
public partial class DeleteLoginPageImageCommandHandler(
    ILoginPageImageRepository repository,
    IFileTransactionTracker fileTransactionTracker,
    ILogger<DeleteLoginPageImageCommandHandler> logger) : IRequestHandler<DeleteLoginPageImageCommand>
{
    public async Task Handle(DeleteLoginPageImageCommand request, CancellationToken cancellationToken)
    {
        LogDeletingImage(logger, request.Id);

        await TryDeleteLoginPageImageFileAsync(request.Id, cancellationToken);

        await repository.DeleteAsync(request.Id, cancellationToken);

        LogImageDeletedSuccessfully(logger, request.Id);
    }

    /// <summary>
    /// Safely attempts to retrieve and delete a login page image file from storage.
    /// Encapsulates exception handling for missing image files or records.
    /// </summary>
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
