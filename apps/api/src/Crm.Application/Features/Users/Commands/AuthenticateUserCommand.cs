using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Auth;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to authenticate a user and retrieve a JWT access token.
/// </summary>
public record AuthenticateUserCommand(
    string Email,
    string Password) : ICommand<AuthTokensDto>, ITransactionalCommand;
