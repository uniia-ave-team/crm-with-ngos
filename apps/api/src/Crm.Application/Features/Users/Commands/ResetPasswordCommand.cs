using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to reset a user's password using a verification token and set a new password.
/// </summary>
public record ResetPasswordCommand(
    string Token,
    string NewPassword) : ICommand, ITransactionalCommand;
