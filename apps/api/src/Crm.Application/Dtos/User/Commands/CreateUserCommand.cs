using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Command to create a new user containing only the essential registration fields.
/// Acts as the DTO for the creation request.
/// </summary>
public record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<Guid>, ITransactionalCommand;
