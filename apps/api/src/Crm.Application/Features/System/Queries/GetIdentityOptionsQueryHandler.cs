using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.System.Queries;
using Crm.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.System.Queries;

/// <summary>
/// Handles the <see cref="GetIdentityOptionsQuery"/> to retrieve system identity configuration and password requirements.
/// </summary>
/// <param name="optionService">The service used to retrieve application configuration options and identity rules.</param>
/// <param name="logger">The logger used to record the execution of the query.</param>
public sealed partial class GetIdentityOptionsQueryHandler(
    IOptionService optionService,
    ILogger<GetIdentityOptionsQueryHandler> logger) : IRequestHandler<GetIdentityOptionsQuery, IdentityOptionsDto>
{
    /// <summary>
    /// Executes the query to retrieve identity options.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the operation, containing the identity options.</returns>
    public Task<IdentityOptionsDto> Handle(GetIdentityOptionsQuery request, CancellationToken cancellationToken)
    {
        LogRetrievingIdentityOptions(logger);

        var options = optionService.GetIdentityOptions();

        LogIdentityOptionsRetrieved(logger);

        return Task.FromResult(options);
    }

    [LoggerMessage(EventId = LogEventIds.RetrievingIdentityOptions, Level = LogLevel.Information, Message = "Retrieving system identity options and password requirements.")]
    private static partial void LogRetrievingIdentityOptions(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.IdentityOptionsRetrieved, Level = LogLevel.Information, Message = "Successfully retrieved identity options.")]
    private static partial void LogIdentityOptionsRetrieved(ILogger logger);
}
