using MediatR;

namespace Crm.Application.Dtos.User.Queries;

/// <summary>
/// Represents a query to retrieve the detailed profile of a specific user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="IsSelf">A value indicating whether the request is made by the user for their own profile.</param>
public record GetUserProfileQuery(
    Guid UserId,
    bool IsSelf = false) : IRequest<UserProfileDto>;
