using Crm.Application.Dtos.Auth;
using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Command to complete user registration using a valid invitation token.
/// Acts as the DTO for the final registration form submission.
/// </summary>
public record CompleteRegistrationCommand(
    string Token,
    string FirstName,
    string LastName,
    string Password,
    string ConfirmPassword) : IRequest<AuthTokensDto>, ITransactionalCommand;
