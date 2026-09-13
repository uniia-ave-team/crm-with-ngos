using MediatR;

namespace Crm.Application.Dtos.Role.Commands;
/// <summary>
/// Represents a command to add a specific permission claim to an existing role.
/// </summary>
/// <param name="RoleId">The unique identifier of the role.</param>
/// <param name="ClaimValue">The value of the permission claim (e.g., "Permissions.CreateUser").</param>
public record AddClaimToRoleCommand(Guid RoleId, string ClaimValue) : IRequest;
