namespace Crm.Application.Dtos.User;

/// <summary>
/// Represents a command to update the profile details of the currently authenticated user.
/// </summary>
/// <param name="FirstName">The updated first name.</param>
/// <param name="LastName">The updated last name.</param>
/// <param name="Patronymic">The updated patronymic.</param>
/// <param name="InternalPosition">The updated internal position or job title within the NGO.</param>
/// <param name="PhoneNumber">The updated phone number.</param>
/// <param name="Country">The updated country of residence.</param>
/// <param name="EmergencyContact">The updated emergency contact information.</param>
/// <param name="PreferredLanguage">The updated preferred interface language.</param>
public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Patronymic,
    string? InternalPosition,
    string? PhoneNumber,
    string? Country,
    string? EmergencyContact,
    string? PreferredLanguage);
