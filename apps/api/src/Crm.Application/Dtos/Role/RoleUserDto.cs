namespace Crm.Application.Dtos.Role;

/// <summary>
/// Represents a data transfer object containing localized role information for a user context.
/// </summary>
/// <param name="Id">The unique identifier of the role.</param>
/// <param name="Name">The name of the role (resolved based on user's pronoun category).</param>
public record RoleUserDto(
    Guid Id,
    string Name);
