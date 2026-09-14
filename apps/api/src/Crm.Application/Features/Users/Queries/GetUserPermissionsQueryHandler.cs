using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUserPermissionsQuery"/> to retrieve all unique access rights
/// from the high-performance cache based on the provided list of Role IDs.
/// </summary>
public partial class GetUserPermissionsQueryHandler(
    IRolePermissionsCache permissionsCache,
    ILogger<GetUserPermissionsQueryHandler> logger) : IRequestHandler<GetUserPermissionsQuery, List<string>>
{
    public async Task<List<string>> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        if (request.RoleIds == null || request.RoleIds.Count == 0)
        {
            LogNoRoleIdsProvided(logger);
            return [];
        }

        LogFetchingPermissions(logger, request.RoleIds.Count);

        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleId in request.RoleIds)
        {
            var rolePermissions = await permissionsCache.GetRolePermissionsAsync(roleId, cancellationToken);

            foreach (var permission in rolePermissions)
            {
                allPermissions.Add(permission);
            }
        }

        var permissionsList = allPermissions.ToList();

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
