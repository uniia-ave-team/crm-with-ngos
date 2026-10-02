namespace Crm.Application.Dtos.User;

/// <summary>
/// Represents the user's active status data transferred to the presentation layer.
/// </summary>
/// <param name="Id">The unique identifier of the user.</param>
/// <param name="Email">The email address associated with the user account.</param>
/// <param name="IsActive">Indicates whether the user account is currently active.</param>
public record UserStatusDto(
    Guid Id,
    string Email,
    bool IsActive);
