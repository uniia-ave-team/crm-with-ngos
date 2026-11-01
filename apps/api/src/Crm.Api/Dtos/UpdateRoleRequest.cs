using Crm.Application.Dtos.Role.Commands;

namespace Crm.Api.Dtos;

/// <summary>
/// Request DTO for updating a role's details without duplicating the ID in the body.
/// </summary>
/// <param name="NewName">The new name to assign to the role.</param>
/// <param name="FeminitiveName">The optional feminitive form of the role name.</param>
/// <param name="PluralName">The optional plural form of the role name.</param>
public record UpdateRoleRequest(
    string NewName,
    string? FeminitiveName,
    string? PluralName)
{
    /// <summary>
    /// Maps the request payload to an <see cref="UpdateRoleCommand"/> instance.
    /// </summary>
    /// <param name="id">The unique identifier of the role from the route.</param>
    public UpdateRoleCommand ToCommand(Guid id) =>
        new(id, NewName, FeminitiveName, PluralName);
}
