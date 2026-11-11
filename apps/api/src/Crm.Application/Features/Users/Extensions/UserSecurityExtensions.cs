using Crm.Application.Dtos.User;

namespace Crm.Application.Features.Users.Extensions;

/// <summary>
/// Provides security and data privacy extension methods for user data.
/// </summary>
public static class UserSecurityExtensions
{
    /// <summary>
    /// Protects emergency contact information by masking it to null if the requester is neither
    /// the user themselves nor authorized with global emergency contact access.
    /// </summary>
    /// <param name="emergencyContact">The raw emergency contact value.</param>
    /// <param name="isSelf">Indicates whether the current requester is the target user.</param>
    /// <param name="hasGlobalAccess">Indicates whether the current requester has permission to view emergency contacts.</param>
    /// <returns>The emergency contact string if authorized; otherwise, <c>null</c>.</returns>
    public static string? ProtectEmergencyContact(string? emergencyContact, bool isSelf, bool hasGlobalAccess)
        => isSelf || hasGlobalAccess ? emergencyContact : null;

    /// <summary>
    /// Protects custom fields by filtering out private entries if the requester is neither
    /// the user themselves nor authorized with global custom fields access.
    /// </summary>
    /// <param name="customFields">The raw collection of custom field DTOs.</param>
    /// <param name="isSelf">Indicates whether the current requester is the target user.</param>
    /// <param name="hasGlobalAccess">Indicates whether the current requester has permission to view private custom fields.</param>
    /// <returns>The collection of custom fields visible to the requester.</returns>
    public static IEnumerable<UserCustomFieldDto> ProtectCustomFields(
        this IEnumerable<UserCustomFieldDto> customFields,
        bool isSelf,
        bool hasGlobalAccess)
        => isSelf || hasGlobalAccess ? customFields : customFields.Where(cf => cf.IsPublic);
}
