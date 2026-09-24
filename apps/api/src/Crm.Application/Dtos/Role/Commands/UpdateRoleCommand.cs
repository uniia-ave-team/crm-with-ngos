using MediatR;

namespace Crm.Application.Dtos.Role.Commands;

/// <summary>
/// Represents a command to update an existing role's details.
/// </summary>
/// <param name="Id">The unique identifier of the role to update.</param>
/// <param name="NewName">The new name to assign to the role.</param>
/// <param name="FeminitiveName">The optional feminitive form of the role name.</param>
/// <param name="PluralName">The optional plural form of the role name.</param>
public record UpdateRoleCommand(
    Guid Id,
    string NewName,
    string? FeminitiveName,
    string? PluralName) : IRequest;
