using MediatR;

namespace Crm.Application.Dtos.Role.Commands;
/// <summary>
/// Represents a command to create a new Role in the system.
/// </summary>
/// <param name="Name">The name of the role to be created.</param>
public record CreateRoleCommand(string Name) : IRequest<Guid>;
