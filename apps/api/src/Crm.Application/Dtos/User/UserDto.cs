using Crm.Application.Dtos.Role;
using Crm.Domain.Enums;

namespace Crm.Application.Dtos.User;

/// <summary>
/// Represents the user data transferred to the presentation layer for paginated user lists.
/// </summary>
/// <param name="Id">The unique identifier of the user.</param>
/// <param name="Email">The email address associated with the user account.</param>
/// <param name="FirstName">The first name of the user.</param>
/// <param name="LastName">The last name of the user.</param>
/// <param name="Patronymic">The optional patronymic (middle name) of the user.</param>
/// <param name="PhoneNumber">The contact phone number of the user.</param>
/// <param name="Country">The country of residence or origin for the user.</param>
/// <param name="EmergencyContact">The emergency contact details, or null if the requester is unauthorized.</param>
/// <param name="Pronouns">The preferred pronouns of the user (e.g., "he/him", "she/her").</param>
/// <param name="PronounCategory">The grammatical category of the user's pronouns.</param>
/// <param name="AvatarUrl">The resolved URL or API endpoint to access the user's avatar image.</param>
/// <param name="Roles">The collection of roles assigned to the user, localized to their pronoun category.</param>
public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Patronymic,
    string? PhoneNumber,
    string? Country,
    string? EmergencyContact,
    string? Pronouns,
    PronounCategory? PronounCategory,
    string? AvatarUrl,
    IEnumerable<RoleUserDto> Roles);
