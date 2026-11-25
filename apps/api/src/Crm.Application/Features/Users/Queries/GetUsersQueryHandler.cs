using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUsersQuery"/> to retrieve a paginated, filtered, and sorted list of organizational users,
/// including custom fields per user with privacy-enforced JSONB search and typed sorting.
/// </summary>
public sealed partial class GetUsersQueryHandler(
    IAuthUserRepository userRepository,
    IAuthRoleRepository authRoleRepository,
    IUserCustomFieldRepository userCustomFieldRepository,
    IPermissionService permissionService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetUsersQueryHandler> logger) : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<UserDto>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        LogFetchingUsers(logger, query.PageNumber, query.PageSize, query.SearchTerm is not null);

        var context = await query.ResolveQueryContextAsync(permissionService, userCustomFieldRepository, cancellationToken);

        var pagedResult = await userRepository.GetPagedAsync<UserDto>(
            searchTerm: query.SearchTerm,
            orderBy: context.EffectiveOrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            showDeleted: query.ShowDeleted,
            customFieldSort: context.CustomFieldSort,
            searchablePublicCustomFieldKeys: context.SearchablePublicCustomFieldKeys,
            searchablePrivateCustomFieldKeys: context.SearchablePrivateCustomFieldKeys,
            currentUserId: query.CurrentUserId,
            cancellationToken: cancellationToken);

        if (pagedResult.Items.Count == 0)
        {
            LogUsersFetched(logger, 0, pagedResult.TotalCount);
            return pagedResult;
        }

        var userIds = pagedResult.Items.Select(u => u.Id).Distinct().ToList();
        var rolesByUser = await authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(userIds, cancellationToken);

        var (hasEmergencyContactAccess, hasCustomFieldsAccess) = await context.ResolveRemainingPermissionsAsync(
            query.CurrentUserRoleIds,
            permissionService,
            cancellationToken);

        var enrichedPage = pagedResult.SecureAndEnrich(
            rolesByUser,
            context.AllOrderedCustomFieldKeys,
            context.PublicOrderedCustomFieldKeys,
            fileUrlProvider,
            query.CurrentUserId,
            hasEmergencyContactAccess,
            hasCustomFieldsAccess);

        LogUsersFetched(logger, enrichedPage.Items.Count, enrichedPage.TotalCount);

        return enrichedPage;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUsers, Level = LogLevel.Information, Message = "Fetching users - Page: {PageNumber}, Size: {PageSize}, HasSearchFilter: {HasSearchFilter}")]
    private static partial void LogFetchingUsers(ILogger logger, int pageNumber, int pageSize, bool hasSearchFilter);

    [LoggerMessage(EventId = LogEventIds.UsersFetched, Level = LogLevel.Information, Message = "Successfully retrieved {FetchedCount} users out of {TotalCount} total matched.")]
    private static partial void LogUsersFetched(ILogger logger, int fetchedCount, int totalCount);
}
