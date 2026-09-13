namespace Crm.Application.Dtos.Role;

/// <summary>
/// Request DTO for updating a role's details without duplicating the ID in the body.
/// </summary>
public record UpdateRoleRequest(string NewName);
