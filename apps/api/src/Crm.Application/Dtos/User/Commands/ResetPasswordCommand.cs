using Crm.Application.Interfaces;
using Mediator;

namespace Crm.Application.Dtos.User.Commands;

/// <summary>
/// Command to reset a user's password using a verification token and set a new password.
/// </summary>
public record ResetPasswordCommand(
    string Token,
    string NewPassword) : IRequest, ITransactionalCommand;
