using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Auth;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to refresh an expired access token using a valid refresh token.
/// </summary>
/// <param name="AccessToken">The expired JWT access token.</param>
/// <param name="RefreshToken">The active refresh token assigned to the user.</param>
public record RefreshTokensCommand(
    string AccessToken,
    string RefreshToken) : ICommand<AuthTokensDto>, ITransactionalCommand;
