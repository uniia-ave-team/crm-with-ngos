using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUsersQuery"/> to retrieve a paginated, filtered, and sorted list of organizational users.
/// </summary>
public partial class GetUsersQueryHandler(
    IAuthUserRepository userRepository,
    IAuthRoleRepository authRoleRepository,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetUsersQueryHandler> logger) : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        LogFetchingUsers(logger, request.PageNumber, request.PageSize, request.SearchTerm != null);

        var pagedResult = await userRepository.GetPagedAsync<UserDto>(
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            showDeleted: request.ShowDeleted,
            cancellationToken: cancellationToken);

        if (pagedResult.Items.Count == 0)
        {
            LogUsersFetched(logger, 0, pagedResult.TotalCount);
            return pagedResult;
        }

        var userIds = pagedResult.Items.Select(u => u.Id).Distinct();
        var rolesByUser = await authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(userIds, cancellationToken);

        var currentUserId = currentUserService.GetUserId();
        var currentUserRoleIds = currentUserService.GetRoleIds();

        var hasGlobalEmergencyContactAccess = await permissionService.HasAccessAsync(
            currentUserRoleIds,
            AccessRight.ViewEmergencyContact,
            cancellationToken);

        var enrichedItems = pagedResult.Items
            .Select(user => user.SecureAndEnrich(
                rolesByUser[user.Id],
                fileUrlProvider,
                currentUserId,
                hasGlobalEmergencyContactAccess))
            .ToList();

        LogUsersFetched(logger, enrichedItems.Count, pagedResult.TotalCount);

        return new PagedResult<UserDto>(
            enrichedItems,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUsers, Level = LogLevel.Information, Message = "Fetching users - Page: {PageNumber}, Size: {PageSize}, HasSearchFilter: {HasSearchFilter}")]
    private static partial void LogFetchingUsers(ILogger logger, int pageNumber, int pageSize, bool hasSearchFilter);

    [LoggerMessage(EventId = LogEventIds.UsersFetched, Level = LogLevel.Information, Message = "Successfully retrieved {FetchedCount} users out of {TotalCount} total matched.")]
    private static partial void LogUsersFetched(ILogger logger, int fetchedCount, int totalCount);
}
