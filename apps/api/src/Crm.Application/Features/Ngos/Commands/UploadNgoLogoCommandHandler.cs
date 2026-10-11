using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Ngos.Commands;

/// <summary>
/// Handles the <see cref="UploadNgoLogoCommand"/> to upload a new logo for the organization,
/// replace the old one on the disk, and update the NGO entity.
/// </summary>
/// <param name="ngoRepository">The repository used for accessing and updating the NGO record.</param>
/// <param name="fileStorageService">The service responsible for physical file I/O operations.</param>
/// <param name="fileTransactionTracker">The service responsible for tracking file transactions to ensure consistency and rollback in case of failures.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class UploadNgoLogoCommandHandler(
    INgoRepository ngoRepository,
    IFileStorageService fileStorageService,
    IFileTransactionTracker fileTransactionTracker,
    ILogger<UploadNgoLogoCommandHandler> logger) : IRequestHandler<UploadNgoLogoCommand>
{
    public async Task Handle(UploadNgoLogoCommand request, CancellationToken cancellationToken)
    {
        LogUploadingLogo(logger, request.OriginalFileName);

        var ngo = await ngoRepository.GetForUpdateAsync(cancellationToken);

        string newFileName = await fileStorageService.SaveImageAsync(
            request.ContentStream,
            request.OriginalFileName,
            FileStorageConstants.NgosFolder,
            cancellationToken);

        fileTransactionTracker.RegisterCreatedFile(newFileName, FileStorageConstants.NgosFolder);

        if (!string.IsNullOrWhiteSpace(ngo.Logo))
        {
            fileTransactionTracker.RegisterFileForDeletion(ngo.Logo, FileStorageConstants.NgosFolder);
        }

        ngo.Logo = newFileName;

        LogLogoUploadedSuccessfully(logger, ngo.Id, newFileName);
    }

    [LoggerMessage(EventId = LogEventIds.UploadingNgoLogo, Level = LogLevel.Information, Message = "Initiating logo upload for the NGO. Original file name: {OriginalFileName}")]
    private static partial void LogUploadingLogo(ILogger logger, string originalFileName);

    [LoggerMessage(EventId = LogEventIds.NgoLogoUploadedSuccessfully, Level = LogLevel.Information, Message = "NGO logo successfully uploaded and updated for NGO ID: {NgoId}. New file name: {FileName}")]
    private static partial void LogLogoUploadedSuccessfully(ILogger logger, Guid ngoId, string fileName);
}
