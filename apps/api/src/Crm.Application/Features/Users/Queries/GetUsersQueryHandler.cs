using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUsersQuery"/> to retrieve a paginated, filtered, and sorted list of organizational users.
/// </summary>
public sealed partial class GetUsersQueryHandler(
    IAuthUserRepository userRepository,
    IAuthRoleRepository authRoleRepository,
    IPermissionService permissionService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetUsersQueryHandler> logger) : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<UserDto>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        LogFetchingUsers(logger, query.PageNumber, query.PageSize, query.SearchTerm != null);

        bool isOrderingByEmergencyContact = string.Equals(
            query.OrderBy?.Trim(),
            nameof(UserDto.EmergencyContact),
            StringComparison.OrdinalIgnoreCase);

        bool? precheckedEmergencyContactAccess = isOrderingByEmergencyContact
            ? await permissionService.HasAccessAsync(
                query.CurrentUserRoleIds,
                AccessRight.ViewEmergencyContact,
                cancellationToken)
            : null;

        string? effectiveOrderBy = isOrderingByEmergencyContact && precheckedEmergencyContactAccess == false
            ? null
            : query.OrderBy;

        var pagedResult = await userRepository.GetPagedAsync<UserDto>(
            searchTerm: query.SearchTerm,
            orderBy: effectiveOrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            showDeleted: query.ShowDeleted,
            cancellationToken: cancellationToken);

        if (pagedResult.Items.Count == 0)
        {
            LogUsersFetched(logger, 0, pagedResult.TotalCount);
            return pagedResult;
        }

        var userIds = pagedResult.Items.Select(u => u.Id).Distinct();
        var rolesByUser = await authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(userIds, cancellationToken);

        var hasGlobalEmergencyContactAccess = precheckedEmergencyContactAccess
            ?? await permissionService.HasAccessAsync(
                query.CurrentUserRoleIds,
                AccessRight.ViewEmergencyContact,
                cancellationToken);

        var enrichedItems = pagedResult.Items
            .Select(user => user.SecureAndEnrich(
                rolesByUser[user.Id],
                fileUrlProvider,
                query.CurrentUserId,
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
