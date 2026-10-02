using MediatR;

namespace Crm.Application.Dtos.User.Queries;
/// <summary>
/// Represents a query to retrieve the detailed profile of a specific user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
public record GetUserProfileQuery(Guid UserId) : IRequest<UserProfileDto>;
