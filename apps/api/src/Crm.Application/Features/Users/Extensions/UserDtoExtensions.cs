using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Roles.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Mapster;

namespace Crm.Application.Features.Users.Extensions;

/// <summary>
/// Provides extension methods for transforming and enriching user DTO instances.
/// </summary>
public static class UserDtoExtensions
{
    /// <summary>
    /// Enriches and sanitizes the user DTO with localized role names, resolved avatar URLs,
    /// protected emergency contact details, and schema-ordered, privacy-filtered custom fields.
    /// </summary>
    /// <param name="user">The base user DTO to enrich.</param>
    /// <param name="roles">The collection of roles assigned to the user.</param>
    /// <param name="allOrderedCustomFieldKeys">All custom field schema keys in display order.</param>
    /// <param name="publicOrderedCustomFieldKeys">Public custom field schema keys in display order.</param>
    /// <param name="fileUrlProvider">The service used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the requesting user.</param>
    /// <param name="hasGlobalEmergencyContactAccess">Whether the requesting user has global permission to view emergency contacts.</param>
    /// <param name="hasGlobalCustomFieldsAccess">Whether the requesting user has global permission to view private custom fields.</param>
    /// <returns>A new <see cref="UserDto"/> with enriched and secured properties.</returns>
    public static UserDto SecureAndEnrich(
        this UserDto user,
        IEnumerable<RoleDto> roles,
        IReadOnlyList<string> allOrderedCustomFieldKeys,
        IReadOnlyList<string> publicOrderedCustomFieldKeys,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        bool hasGlobalEmergencyContactAccess,
        bool hasGlobalCustomFieldsAccess)
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
            CustomFields = user.CustomFields.ProtectCustomFields(
                allOrderedCustomFieldKeys,
                publicOrderedCustomFieldKeys,
                isSelf,
                hasGlobalCustomFieldsAccess),
        };
    }

    /// <summary>
    /// Enriches and sanitizes a batch of <see cref="UserDto"/> instances using pre-fetched role lookups and ordered custom field keys.
    /// </summary>
    /// <param name="users">The page of user DTOs to enrich.</param>
    /// <param name="rolesByUser">The lookup mapping user IDs to their roles.</param>
    /// <param name="allOrderedCustomFieldKeys">All custom field schema keys in display order.</param>
    /// <param name="publicOrderedCustomFieldKeys">Public custom field schema keys in display order.</param>
    /// <param name="fileUrlProvider">The service used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the requesting user.</param>
    /// <param name="hasGlobalEmergencyContactAccess">Whether the requesting user can view emergency contacts.</param>
    /// <param name="hasGlobalCustomFieldsAccess">Whether the requesting user can view private custom fields.</param>
    /// <returns>The enriched and sanitized list of user DTOs.</returns>
    public static List<UserDto> SecureAndEnrich(
        this IEnumerable<UserDto> users,
        ILookup<Guid, RoleDto> rolesByUser,
        IReadOnlyList<string> allOrderedCustomFieldKeys,
        IReadOnlyList<string> publicOrderedCustomFieldKeys,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        bool hasGlobalEmergencyContactAccess,
        bool hasGlobalCustomFieldsAccess)
        =>
        [
            .. users.Select(user => user.SecureAndEnrich(
                rolesByUser[user.Id],
                allOrderedCustomFieldKeys,
                publicOrderedCustomFieldKeys,
                fileUrlProvider,
                currentUserId,
                hasGlobalEmergencyContactAccess,
                hasGlobalCustomFieldsAccess)),
        ];

    /// <summary>
    /// Enriches and sanitizes every user in a <see cref="PagedResult{T}"/>, preserving pagination metadata.
    /// </summary>
    /// <param name="pagedResult">The paged result of base user DTOs.</param>
    /// <param name="rolesByUser">The lookup mapping user IDs to their roles.</param>
    /// <param name="allOrderedCustomFieldKeys">All custom field schema keys in display order.</param>
    /// <param name="publicOrderedCustomFieldKeys">Public custom field schema keys in display order.</param>
    /// <param name="fileUrlProvider">The service used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the requesting user.</param>
    /// <param name="hasGlobalEmergencyContactAccess">Whether the requesting user can view emergency contacts.</param>
    /// <param name="hasGlobalCustomFieldsAccess">Whether the requesting user can view private custom fields.</param>
    /// <returns>A new <see cref="PagedResult{T}"/> containing the enriched and sanitized user DTOs.</returns>
    public static PagedResult<UserDto> SecureAndEnrich(
        this PagedResult<UserDto> pagedResult,
        ILookup<Guid, RoleDto> rolesByUser,
        IReadOnlyList<string> allOrderedCustomFieldKeys,
        IReadOnlyList<string> publicOrderedCustomFieldKeys,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        bool hasGlobalEmergencyContactAccess,
        bool hasGlobalCustomFieldsAccess)
        => new(
            pagedResult.Items.SecureAndEnrich(
                rolesByUser,
                allOrderedCustomFieldKeys,
                publicOrderedCustomFieldKeys,
                fileUrlProvider,
                currentUserId,
                hasGlobalEmergencyContactAccess,
                hasGlobalCustomFieldsAccess),
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);

    /// <summary>
    /// Enriches and sanitizes the user profile with localized role names, resolved avatar URLs,
    /// protected emergency contact details, and schema-ordered, privacy-filtered custom fields.
    /// </summary>
    /// <param name="user">The base user profile result to enrich.</param>
    /// <param name="roles">The collection of roles assigned to the user.</param>
    /// <param name="allOrderedCustomFieldKeys">All custom field schema keys in display order.</param>
    /// <param name="publicOrderedCustomFieldKeys">Public custom field schema keys in display order.</param>
    /// <param name="fileUrlProvider">The service used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the requesting user.</param>
    /// <param name="hasGlobalEmergencyContactAccess">Whether the requesting user has global permission to view emergency contacts.</param>
    /// <param name="hasGlobalCustomFieldsAccess">Whether the requesting user has global permission to view private custom fields.</param>
    /// <param name="isExplicitSelf">Optional explicit flag indicating whether the requester is the user.</param>
    /// <returns>A new <see cref="UserProfileDto"/> with enriched and secured properties.</returns>
    public static UserProfileDto SecureAndEnrich(
        this UserProfileResult user,
        IEnumerable<RoleDto> roles,
        IReadOnlyList<string> allOrderedCustomFieldKeys,
        IReadOnlyList<string> publicOrderedCustomFieldKeys,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        bool hasGlobalEmergencyContactAccess,
        bool hasGlobalCustomFieldsAccess,
        bool isExplicitSelf = false)
    {
        bool isSelf = isExplicitSelf || currentUserId == user.Id;

        return user.Adapt<UserProfileDto>() with
        {
            EmergencyContact = UserSecurityExtensions.ProtectEmergencyContact(
                user.EmergencyContact,
                isSelf,
                hasGlobalEmergencyContactAccess),
            AvatarUrl = fileUrlProvider.ResolveUserAvatarUrl(user.AvatarUrl, user.Id, isSelf),
            Roles = [.. roles.ToRoleUserDtos(user.PronounCategory)],
            CustomFields = user.CustomFields.ProtectCustomFields(
                allOrderedCustomFieldKeys,
                publicOrderedCustomFieldKeys,
                isSelf,
                hasGlobalCustomFieldsAccess),
        };
    }
}
