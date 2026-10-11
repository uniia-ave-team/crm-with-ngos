using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Ngos.Commands;

/// <summary>
/// Handles the <see cref="UpdateNgoCommand"/> to update details of an existing NGO.
/// </summary>
/// <param name="repository">The repository used for accessing and persisting NGO data.</param>
/// <param name="fileTransactionTracker">The service used to track file storage transactions for consistency and rollback.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class UpdateNgoCommandHandler(
    INgoRepository repository,
    IFileTransactionTracker fileTransactionTracker,
    ILogger<UpdateNgoCommandHandler> logger) : IRequestHandler<UpdateNgoCommand>
{
    public async Task Handle(UpdateNgoCommand request, CancellationToken cancellationToken)
    {
        var ngo = await repository.GetForUpdateAsync(cancellationToken);

        LogUpdatingNgo(logger, ngo.Id);

        if (request.LogoUrl is not null && ngo.Logo is not null)
        {
            fileTransactionTracker.RegisterFileForDeletion(ngo.Logo, FileStorageConstants.NgosFolder);
        }

        request.Adapt(ngo);

        LogNgoUpdatedSuccessfully(logger, ngo.Id);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingNgo, Level = LogLevel.Information, Message = "Updating details for NGO ID: {NgoId}")]
    private static partial void LogUpdatingNgo(ILogger logger, Guid ngoId);

    [LoggerMessage(EventId = LogEventIds.NgoUpdatedSuccessfully, Level = LogLevel.Information, Message = "NGO ID: {NgoId} details successfully updated.")]
    private static partial void LogNgoUpdatedSuccessfully(ILogger logger, Guid ngoId);
}
