namespace Crm.Application.Dtos.Role;

/// <summary>
/// Represents a data transfer object containing role details.
/// </summary>
/// <param name="Id">The unique identifier of the role.</param>
/// <param name="Name">The name of the role.</param>
public record RoleDto(
    Guid Id,
    string Name);
