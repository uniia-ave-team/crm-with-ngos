using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Features.Roles.Extensions;
using Crm.Application.Interfaces;

namespace Crm.Application.Features.Users.Extensions;

/// <summary>
/// Provides extension methods for transforming and enriching user DTO instances.
/// </summary>
public static class UserDtoExtensions
{
    /// <summary>
    /// Enriches and sanitizes the user DTO with localized role names, resolved avatar URLs,
    /// and protected emergency contact details based on user identity and permissions.
    /// </summary>
    /// <param name="user">The base user DTO to enrich.</param>
    /// <param name="roles">The collection of roles assigned to the user.</param>
    /// <param name="fileUrlProvider">The service used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the requesting user.</param>
    /// <param name="hasGlobalEmergencyContactAccess">Whether the requesting user has global permission to view emergency contacts.</param>
    /// <returns>A new <see cref="UserDto"/> with enriched and secured properties.</returns>
    public static UserDto SecureAndEnrich(
        this UserDto user,
        IEnumerable<RoleDto> roles,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        bool hasGlobalEmergencyContactAccess)
    {
        bool isSelf = currentUserId == user.Id;

        return user with
        {
            Roles = [.. roles.ToRoleUserDtos(user.PronounCategory)],
            EmergencyContact = UserSecurityExtensions.ProtectEmergencyContact(
                user.EmergencyContact,
                isSelf,
                hasGlobalEmergencyContactAccess),
            AvatarUrl = fileUrlProvider.ResolveUserAvatarUrl(user.AvatarUrl, user.Id, isSelf),
        };
    }

    /// <summary>
    /// Enriches and sanitizes the user profile with localized role names, resolved avatar URLs,
    /// protected emergency contact details, and filtered custom fields based on permissions.
    /// </summary>
    /// <param name="user">The base user profile result to enrich.</param>
    /// <param name="roles">The collection of roles assigned to the user.</param>
    /// <param name="fileUrlProvider">The service used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the requesting user.</param>
    /// <param name="hasGlobalEmergencyContactAccess">Whether the requesting user has global permission to view emergency contacts.</param>
    /// <param name="hasGlobalCustomFieldsAccess">Whether the requesting user has global permission to view private custom fields.</param>
    /// <param name="isExplicitSelf">Optional explicit flag indicating whether the requester is the user.</param>
    /// <returns>A new <see cref="UserProfileDto"/> with enriched and secured properties.</returns>
    public static UserProfileDto SecureAndEnrich(
        this UserProfileResult user,
        IEnumerable<RoleDto> roles,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        bool hasGlobalEmergencyContactAccess,
        bool hasGlobalCustomFieldsAccess,
        bool isExplicitSelf = false)
    {
        bool isSelf = isExplicitSelf || currentUserId == user.Id;

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Patronymic,
            user.PhoneNumber,
            user.Country,
            UserSecurityExtensions.ProtectEmergencyContact(
                user.EmergencyContact,
                isSelf,
                hasGlobalEmergencyContactAccess),
            user.PreferredLanguage,
            user.Pronouns,
            fileUrlProvider.ResolveUserAvatarUrl(user.AvatarUrl, user.Id, isSelf),
            [.. roles.ToRoleUserDtos(user.PronounCategory)],
            [.. user.CustomFields.ProtectCustomFields(isSelf, hasGlobalCustomFieldsAccess)]);
    }
}
