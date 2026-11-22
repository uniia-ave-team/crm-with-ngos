using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Represents a query to retrieve the detailed profile of a specific user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="CurrentUserId">The unique identifier of the currently authenticated user.</param>
/// <param name="CurrentUserRoleIds">The role identifiers of the currently authenticated user.</param>
/// <param name="IsSelf">A value indicating whether the request is made by the user for their own profile.</param>
public record GetUserProfileQuery(
    Guid UserId,
    Guid CurrentUserId,
    List<Guid> CurrentUserRoleIds,
    bool IsSelf = false) : IQuery<UserProfileDto>;
