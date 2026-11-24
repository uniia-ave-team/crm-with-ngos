using Crm.Application.Common.Consts;
using Crm.Domain.Extensions;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Handles the <see cref="GetAvailableAccessRightsQuery"/> to return all system-defined access rights.
/// </summary>
/// <param name="logger">The logger used to record the execution of the query.</param>
public sealed partial class GetAvailableAccessRightsQueryHandler(
    ILogger<GetAvailableAccessRightsQueryHandler> logger) : IQueryHandler<GetAvailableAccessRightsQuery, IReadOnlySet<string>>
{
    public ValueTask<IReadOnlySet<string>> Handle(GetAvailableAccessRightsQuery query, CancellationToken cancellationToken)
    {
        LogFetchingAvailableAccessRights(logger);

        var rights = PermissionExtensions.GetAllStringValues();

        return ValueTask.FromResult(rights);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingAvailableAccessRights, Level = LogLevel.Information, Message = "Fetching all available system access rights for UI.")]
    private static partial void LogFetchingAvailableAccessRights(ILogger logger);
}
