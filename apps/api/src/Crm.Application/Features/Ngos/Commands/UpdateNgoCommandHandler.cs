using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Ngos.Commands;

/// <summary>
/// Handles the <see cref="UpdateNgoCommand"/> to update details of an existing NGO.
/// </summary>
/// <param name="repository">The repository used for accessing and persisting NGO data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class UpdateNgoCommandHandler(
    INgoRepository repository,
    ILogger<UpdateNgoCommandHandler> logger) : ICommandHandler<UpdateNgoCommand>
{
    public async ValueTask<Unit> Handle(UpdateNgoCommand command, CancellationToken cancellationToken)
    {
        var ngo = await repository.GetForUpdateAsync(cancellationToken);

        LogUpdatingNgo(logger, ngo.Id);

        command.Adapt(ngo);

        LogNgoUpdatedSuccessfully(logger, ngo.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingNgo, Level = LogLevel.Information, Message = "Updating details for NGO ID: {NgoId}")]
    private static partial void LogUpdatingNgo(ILogger logger, Guid ngoId);

    [LoggerMessage(EventId = LogEventIds.NgoUpdatedSuccessfully, Level = LogLevel.Information, Message = "NGO ID: {NgoId} details successfully updated.")]
    private static partial void LogNgoUpdatedSuccessfully(ILogger logger, Guid ngoId);
}
