using Crm.Application.Dtos.Auth;
using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Represents a command to refresh an expired access token using a valid refresh token.
/// </summary>
/// <param name="AccessToken">The expired JWT access token.</param>
/// <param name="RefreshToken">The active refresh token assigned to the user.</param>
public record RefreshTokensCommand(
    string AccessToken,
    string RefreshToken) : IRequest<AuthTokensDto>, ITransactionalCommand;
