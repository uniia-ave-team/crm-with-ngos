using Crm.Application.Dtos.Role;

namespace Crm.Application.Dtos.User;

/// <summary>
/// Represents the detailed profile data of a user transferred to the presentation layer.
/// </summary>
/// <param name="Id">The unique identifier of the user.</param>
/// <param name="Email">The email address associated with the user account.</param>
/// <param name="FirstName">The first name of the user.</param>
/// <param name="LastName">The last name of the user.</param>
/// <param name="Patronymic">The patronymic of the user.</param>
/// <param name="InternalPosition">The internal position or job title of the user within the NGO.</param>
/// <param name="PhoneNumber">The phone number of the user.</param>
/// <param name="Country">The country of residence of the user.</param>
/// <param name="EmergencyContact">The emergency contact information for the user.</param>
/// <param name="PreferredLanguage">The preferred interface language of the user.</param>
/// <param name="Pronouns">The preferred pronouns of the user.</param>
/// <param name="AvatarUrl">The URL or file path to the user's avatar.</param>
/// <param name="Roles">The collection of role names assigned to the user.</param>
public record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Patronymic,
    string? InternalPosition,
    string? PhoneNumber,
    string? Country,
    string? EmergencyContact,
    string? PreferredLanguage,
    string? Pronouns,
    string? AvatarUrl,
    IEnumerable<RoleDto> Roles);
