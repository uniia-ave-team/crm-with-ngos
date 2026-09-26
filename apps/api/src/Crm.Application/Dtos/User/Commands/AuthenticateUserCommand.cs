using Crm.Application.Dtos.Auth;
using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Command to authenticate a user and retrieve a JWT access token.
/// </summary>
public record AuthenticateUserCommand(
    string Email,
    string Password) : IRequest<AuthTokensDto>, ITransactionalCommand;
