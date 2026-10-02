using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Represents a command to log out a user by invalidating their current refresh token.
/// </summary>
/// <param name="UserId">The unique identifier of the user to log out.</param>
/// <param name="RefreshToken">The refresh token to invalidate.</param>
public record LogoutCommand(Guid UserId, string RefreshToken) : IRequest;
