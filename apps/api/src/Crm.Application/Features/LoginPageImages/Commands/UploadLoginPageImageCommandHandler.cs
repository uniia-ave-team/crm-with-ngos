using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Commands;

/// <summary>
/// Handles the <see cref="UploadLoginPageImageCommand"/> to upload a new login page image file,
/// save it via IFileStorageService, and create its database record associated with the NGO.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="ngoRepository">The repository used to retrieve the primary Non-Governmental Organization (NGO).</param>
/// <param name="fileTransactionTracker">The service responsible for tracking file transactions to ensure consistency and rollback in case of failures.</param>
/// <param name="fileStorageService">The service responsible for physical file I/O operations.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the login page image upload process.</param>
public partial class UploadLoginPageImageCommandHandler(
    ILoginPageImageRepository repository,
    INgoRepository ngoRepository,
    IFileTransactionTracker fileTransactionTracker,
    IFileStorageService fileStorageService,
    ILogger<UploadLoginPageImageCommandHandler> logger) : IRequestHandler<UploadLoginPageImageCommand, Guid>
{
    public async Task<Guid> Handle(UploadLoginPageImageCommand request, CancellationToken cancellationToken)
    {
        LogUploadingImage(logger, request.OriginalFileName);

        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        string savedFileName = await fileStorageService.SaveImageAsync(
            request.ContentStream,
            request.OriginalFileName,
            FileStorageConstants.LoginPageImagesFolder,
            cancellationToken);

        fileTransactionTracker.RegisterCreatedFile(savedFileName, FileStorageConstants.LoginPageImagesFolder);

        var loginPageImage = new LoginPageImage
        {
            Url = savedFileName,
            NgoId = ngoId,
        };

        await repository.CreateAsync(loginPageImage, cancellationToken);

        LogImageUploadedSuccessfully(logger, loginPageImage.Id, savedFileName);

        return loginPageImage.Id;
    }

    [LoggerMessage(EventId = LogEventIds.UploadingLoginPageImage, Level = LogLevel.Information, Message = "Initiating upload for login page image. Original file name: {OriginalFileName}")]
    private static partial void LogUploadingImage(ILogger logger, string originalFileName);

    [LoggerMessage(EventId = LogEventIds.LoginPageImageUploadedSuccessfully, Level = LogLevel.Information, Message = "Login page image successfully uploaded and created with ID: {ImageId}. File name: {FileName}")]
    private static partial void LogImageUploadedSuccessfully(ILogger logger, Guid imageId, string fileName);
}
