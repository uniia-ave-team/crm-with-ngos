using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Roles.Extensions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUserProfileQuery"/> to retrieve specific user profile details.
/// </summary>
public partial class GetUserProfileQueryHandler(
    IUserRepository userRepository,
    IAuthRoleRepository authRoleRepository,
    ILogger<GetUserProfileQueryHandler> logger) : IRequestHandler<GetUserProfileQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        LogFetchingUserProfile(logger, request.UserId);

        var user = await userRepository.GetAsync<UserProfileDto>(request.UserId, cancellationToken);

        var roles = await authRoleRepository.GetRolesByUserAsync<RoleDto>(request.UserId, cancellationToken);

        LogUserProfileFetched(logger, request.UserId);

        return user with { Roles = roles.Select(r => r.ResolveNameForUser(user.PronounCategory)) };
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUserProfile, Level = LogLevel.Information, Message = "Fetching profile for user ID: {UserId}")]
    private static partial void LogFetchingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileFetched, Level = LogLevel.Information, Message = "Successfully retrieved profile for user ID: {UserId}")]
    private static partial void LogUserProfileFetched(ILogger logger, Guid userId);
}
