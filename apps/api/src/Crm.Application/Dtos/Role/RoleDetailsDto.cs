namespace Crm.Application.Dtos.Role;

/// <summary>
/// Represents detailed information about a role, including its associated claims.
/// </summary>
/// <param name="Id">The unique identifier of the role.</param>
/// <param name="Name">The name of the role.</param>
/// <param name="Claims">A collection of claim values (permissions) associated with the role.</param>
public record RoleDetailsDto(
    Guid Id,
    string Name,
    IEnumerable<string> Claims);
