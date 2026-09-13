namespace Crm.Application.Dtos.Role;

/// <summary>
/// Request DTO for adding a claim to a role without duplicating the role ID in the body.
/// </summary>
public record AddRoleClaimRequest(string ClaimValue);
