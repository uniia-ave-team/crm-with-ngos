using MediatR;

namespace Crm.Application.Dtos.Role.Commands;
/// <summary>
/// Represents a command to delete an existing role.
/// </summary>
/// <param name="Id">The unique identifier of the role to delete.</param>
public record DeleteRoleCommand(Guid Id) : IRequest;
