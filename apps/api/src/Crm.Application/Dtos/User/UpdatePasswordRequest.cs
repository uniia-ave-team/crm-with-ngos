namespace Crm.Application.Dtos.User;

/// <summary>
/// Command to update the password for the currently authenticated user.
/// </summary>
/// <param name="CurrentPassword">The current password of the user.</param>
/// <param name="NewPassword">The new password to set.</param>
public record UpdatePasswordRequest(
    string CurrentPassword,
    string NewPassword);
