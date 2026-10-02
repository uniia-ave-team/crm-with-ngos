using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role.Queries;
using Crm.Domain.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Handles the <see cref="GetAvailableAccessRightsQuery"/> to return all system-defined access rights.
/// </summary>
public partial class GetAvailableAccessRightsQueryHandler(
    ILogger<GetAvailableAccessRightsQueryHandler> logger) : IRequestHandler<GetAvailableAccessRightsQuery, IReadOnlySet<string>>
{
    public Task<IReadOnlySet<string>> Handle(GetAvailableAccessRightsQuery request, CancellationToken cancellationToken)
    {
        LogFetchingAvailableAccessRights(logger);

        var rights = PermissionExtensions.GetAllStringValues();

        return Task.FromResult(rights);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingAvailableAccessRights, Level = LogLevel.Information, Message = "Fetching all available system access rights for UI.")]
    private static partial void LogFetchingAvailableAccessRights(ILogger logger);
}
