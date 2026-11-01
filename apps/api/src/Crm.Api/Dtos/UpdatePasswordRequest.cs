using Crm.Application.Dtos.User.Commands;

namespace Crm.Api.Dtos;

/// <summary>
/// Command to update the password for the currently authenticated user.
/// </summary>
/// <param name="CurrentPassword">The current password of the user.</param>
/// <param name="NewPassword">The new password to set.</param>
public record UpdatePasswordRequest(
    string CurrentPassword,
    string NewPassword)
{
    /// <summary>
    /// Maps the request payload to an <see cref="UpdatePasswordCommand"/> instance.
    /// </summary>
    /// <param name="userId">The unique identifier of the user from the route or authentication context.</param>
    public UpdatePasswordCommand ToCommand(Guid userId) =>
        new(userId, CurrentPassword, NewPassword);
}
