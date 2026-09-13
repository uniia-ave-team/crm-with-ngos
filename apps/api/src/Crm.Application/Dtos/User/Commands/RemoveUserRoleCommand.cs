using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Represents a command to remove a specific role from a user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="RoleId">The unique identifier of the role to remove.</param>
public record RemoveUserRoleCommand(
    Guid UserId,
    Guid RoleId) : IRequest;
