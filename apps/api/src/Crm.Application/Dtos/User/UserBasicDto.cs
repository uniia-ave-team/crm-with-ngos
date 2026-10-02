namespace Crm.Application.Dtos.User;

/// <summary>
/// Represents the basic user identity data transferred to the presentation layer.
/// </summary>
public record UserBasicDto(
    Guid Id,
    string Email);
