namespace Crm.Application.Dtos.User;

/// <summary>
/// Represents the user data transferred to the presentation layer.
/// </summary>
public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName);
