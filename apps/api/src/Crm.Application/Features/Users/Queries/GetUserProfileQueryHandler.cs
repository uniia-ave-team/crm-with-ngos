using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUserProfileQuery"/> to retrieve specific user profile details.
/// </summary>
public sealed partial class GetUserProfileQueryHandler(
    IAuthUserRepository authUserRepository,
    IAuthRoleRepository authRoleRepository,
    IPermissionService permissionService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetUserProfileQueryHandler> logger) : IQueryHandler<GetUserProfileQuery, UserProfileDto>
{
    public async ValueTask<UserProfileDto> Handle(GetUserProfileQuery query, CancellationToken cancellationToken)
    {
        LogFetchingUserProfile(logger, query.UserId);

        var user = await authUserRepository.GetAsync<UserProfileResult>(query.UserId, cancellationToken);
        var roles = await authRoleRepository.GetRolesByUserAsync<RoleDto>(query.UserId, cancellationToken);

        LogUserProfileFetched(logger, query.UserId);

        var isSelf = query.IsSelf || query.CurrentUserId == query.UserId;

        bool hasGlobalEmergencyContactAccess = false;
        bool hasGlobalCustomFieldsAccess = false;

        if (!isSelf)
        {
            hasGlobalEmergencyContactAccess = await permissionService.HasAccessAsync(
                query.CurrentUserRoleIds,
                AccessRight.ViewEmergencyContact,
                cancellationToken);

            hasGlobalCustomFieldsAccess = await permissionService.HasAccessAsync(
                query.CurrentUserRoleIds,
                AccessRight.ViewCustomFields,
                cancellationToken);
        }

        return user.SecureAndEnrich(
            roles,
            fileUrlProvider,
            query.CurrentUserId,
            hasGlobalEmergencyContactAccess,
            hasGlobalCustomFieldsAccess,
            isSelf);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUserProfile, Level = LogLevel.Information, Message = "Fetching profile for user ID: {UserId}")]
    private static partial void LogFetchingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileFetched, Level = LogLevel.Information, Message = "Successfully retrieved profile for user ID: {UserId}")]
    private static partial void LogUserProfileFetched(ILogger logger, Guid userId);
}
