using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Ngos.Commands;

/// <summary>
/// Handles the <see cref="UpdateNgoCommand"/> to update details of an existing NGO.
/// </summary>
/// <param name="repository">The repository used for accessing and persisting NGO data.</param>
public partial class UpdateNgoCommandHandler(
    INgoRepository repository,
    ILogger<UpdateNgoCommandHandler> logger) : IRequestHandler<UpdateNgoCommand>
{
    public async Task Handle(UpdateNgoCommand request, CancellationToken cancellationToken)
    {
        var ngo = await repository.GetAsync(cancellationToken);

        LogUpdatingNgo(logger, ngo.Id);

        repository.Update(request.Adapt(ngo));

        LogNgoUpdatedSuccessfully(logger, ngo.Id);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingNgo, Level = LogLevel.Information, Message = "Updating details for NGO ID: {NgoId}")]
    private static partial void LogUpdatingNgo(ILogger logger, Guid ngoId);

    [LoggerMessage(EventId = LogEventIds.NgoUpdatedSuccessfully, Level = LogLevel.Information, Message = "NGO ID: {NgoId} details successfully updated.")]
    private static partial void LogNgoUpdatedSuccessfully(ILogger logger, Guid ngoId);
}
