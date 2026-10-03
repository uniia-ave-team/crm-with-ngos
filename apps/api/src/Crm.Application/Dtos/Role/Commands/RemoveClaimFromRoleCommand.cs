using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.Role.Commands;
/// <summary>
/// Represents a command to remove a specific permission claim from an existing role.
/// </summary>
/// <param name="RoleId">The unique identifier of the role.</param>
/// <param name="ClaimValue">The value of the permission claim to remove (e.g., "Permissions.CreateUser").</param>
public record RemoveClaimFromRoleCommand(Guid RoleId, string ClaimValue) : IRequest, ITransactionalCommand;
