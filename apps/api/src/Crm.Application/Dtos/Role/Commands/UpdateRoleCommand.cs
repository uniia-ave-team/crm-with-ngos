using MediatR;

namespace Crm.Application.Dtos.Role.Commands;
/// <summary>
/// Represents a command to update an existing role's details.
/// </summary>
/// <param name="Id">The unique identifier of the role to update.</param>
/// <param name="NewName">The new name to assign to the role.</param>
public record UpdateRoleCommand(Guid Id, string NewName) : IRequest;
