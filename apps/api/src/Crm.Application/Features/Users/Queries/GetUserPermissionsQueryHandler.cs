using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUserPermissionsQuery"/> to retrieve all unique access rights
/// from the high-performance cache based on the provided list of Role IDs.
/// </summary>
public sealed partial class GetUserPermissionsQueryHandler(
    IRolePermissionsCache permissionsCache,
    ILogger<GetUserPermissionsQueryHandler> logger) : IQueryHandler<GetUserPermissionsQuery, List<string>>
{
    public async ValueTask<List<string>> Handle(GetUserPermissionsQuery query, CancellationToken cancellationToken)
    {
        if (query.RoleIds == null || query.RoleIds.Count == 0)
        {
            LogNoRoleIdsProvided(logger);
            return [];
        }

        LogFetchingPermissions(logger, query.RoleIds.Count);

        var tasks = query.RoleIds.Select(id => permissionsCache.GetRolePermissionsAsync(id, cancellationToken));
        var results = await Task.WhenAll(tasks);

        var permissionsList = results
            .SelectMany(r => r)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        LogPermissionsFetchedSuccessfully(logger, permissionsList.Count);

        return permissionsList;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUserPermissions, Level = LogLevel.Information, Message = "Fetching permissions for {RoleCount} role(s).")]
    private static partial void LogFetchingPermissions(ILogger logger, int roleCount);

    [LoggerMessage(EventId = LogEventIds.NoRoleIdsProvidedForPermissions, Level = LogLevel.Warning, Message = "No Role IDs provided in the query. Returning empty permissions list.")]
    private static partial void LogNoRoleIdsProvided(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.PermissionsFetchedSuccessfully, Level = LogLevel.Information, Message = "Successfully fetched {PermissionCount} unique permissions.")]
    private static partial void LogPermissionsFetchedSuccessfully(ILogger logger, int permissionCount);
}
