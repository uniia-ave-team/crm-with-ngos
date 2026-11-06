using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
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
public partial class GetUserProfileQueryHandler(
    IAuthUserRepository authUserRepository,
    IAuthRoleRepository authRoleRepository,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetUserProfileQueryHandler> logger) : IRequestHandler<GetUserProfileQuery, UserProfileDto>
{
    public async ValueTask<UserProfileDto> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        LogFetchingUserProfile(logger, request.UserId);

        var user = await authUserRepository.GetAsync<UserProfileResult>(request.UserId, cancellationToken);
        var roles = await authRoleRepository.GetRolesByUserAsync<RoleDto>(request.UserId, cancellationToken);

        LogUserProfileFetched(logger, request.UserId);

        var currentUserId = currentUserService.GetUserId();
        var isSelf = request.IsSelf || currentUserId == request.UserId;

        bool hasGlobalEmergencyContactAccess = false;
        bool hasGlobalCustomFieldsAccess = false;

        if (!isSelf)
        {
            var currentUserRoleIds = currentUserService.GetRoleIds();

            hasGlobalEmergencyContactAccess = await permissionService.HasAccessAsync(
                currentUserRoleIds,
                AccessRight.ViewEmergencyContact,
                cancellationToken);

            hasGlobalCustomFieldsAccess = await permissionService.HasAccessAsync(
                currentUserRoleIds,
                AccessRight.ViewCustomFields,
                cancellationToken);
        }

        return user.SecureAndEnrich(
            roles,
            fileUrlProvider,
            currentUserId,
            hasGlobalEmergencyContactAccess,
            hasGlobalCustomFieldsAccess,
            isSelf);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUserProfile, Level = LogLevel.Information, Message = "Fetching profile for user ID: {UserId}")]
    private static partial void LogFetchingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileFetched, Level = LogLevel.Information, Message = "Successfully retrieved profile for user ID: {UserId}")]
    private static partial void LogUserProfileFetched(ILogger logger, Guid userId);
}
