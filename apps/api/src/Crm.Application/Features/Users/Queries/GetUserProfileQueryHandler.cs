using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Roles.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
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
    public async Task<UserProfileDto> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        LogFetchingUserProfile(logger, request.UserId);

        var user = await authUserRepository.GetAsync<UserProfileResult>(request.UserId, cancellationToken);

        var roles = await authRoleRepository.GetRolesByUserAsync<RoleDto>(request.UserId, cancellationToken);

        LogUserProfileFetched(logger, request.UserId);

        var currentUserId = currentUserService.GetUserId();
        var currentUserRoleIds = currentUserService.GetRoleIds();
        var isSelf = currentUserId == request.UserId;

        var canViewEmergencyContact = isSelf ||
            await permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, cancellationToken);

        var canViewCustomFields = isSelf ||
            await permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, cancellationToken);

        var securedUserResult = user with
        {
            Roles = roles.Select(r => r.ResolveNameForUser(user.PronounCategory)),
            EmergencyContact = canViewEmergencyContact ? user.EmergencyContact : null,
            CustomFields = canViewCustomFields ? user.CustomFields : user.CustomFields.Where(cf => cf.IsPublic),
            AvatarUrl = ResolveAvatarUrl(user, request.IsSelf),
        };

        return securedUserResult.Adapt<UserProfileDto>();
    }

    private string? ResolveAvatarUrl(UserProfileResult user, bool isSelf)
    {
        if (string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            return null;
        }

        string routeTemplate = isSelf
            ? ApiRouteLogoConstants.UserSelfAvatar
            : ApiRouteLogoConstants.UserAvatar(user.Id);

        return fileUrlProvider.GetFileUrl(user.AvatarUrl, routeTemplate);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUserProfile, Level = LogLevel.Information, Message = "Fetching profile for user ID: {UserId}")]
    private static partial void LogFetchingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileFetched, Level = LogLevel.Information, Message = "Successfully retrieved profile for user ID: {UserId}")]
    private static partial void LogUserProfileFetched(ILogger logger, Guid userId);
}
