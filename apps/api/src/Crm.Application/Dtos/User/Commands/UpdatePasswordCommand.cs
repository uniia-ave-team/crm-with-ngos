using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Command to update the password for an existing user.
/// </summary>
public record UpdatePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest;
