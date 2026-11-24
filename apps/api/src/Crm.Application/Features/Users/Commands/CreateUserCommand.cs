using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to create a new user containing only the essential registration fields.
/// Acts as the DTO for the creation request.
/// </summary>
public record CreateUserCommand(
    string FirstName,
    string LastName,
    string? Patronymic,
    string Email,
    string Password,
    string ConfirmPassword) : ICommand<Guid>, ITransactionalCommand;
