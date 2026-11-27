using System.Text.Json.Nodes;
using Crm.Application.Features.Users.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Mapster;

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
    /// Applies profile updates from <paramref name="command"/> to <paramref name="user"/> via Mapster,
    /// preserving the existing <see cref="User.EmergencyContact"/> unchanged when <paramref name="canEditEmergencyContact"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="user">The tracked target user entity to update.</param>
    /// <param name="command">The incoming profile update command.</param>
    /// <param name="canEditEmergencyContact">
    /// Whether the caller is allowed to modify <see cref="User.EmergencyContact"/>
    /// (self-update or holding <see cref="Domain.Enums.AccessRight.ViewEmergencyContact"/>).
    /// </param>
    public static void ApplyProfileUpdate(
        this User user,
        UpdateUserProfileCommand command,
        bool canEditEmergencyContact)
    {
        var effectiveCommand = canEditEmergencyContact
            ? command
            : command with { EmergencyContact = user.EmergencyContact };

        effectiveCommand.Adapt(user);
    }

    /// <summary>
    /// Protects and orders a user's custom fields <see cref="JsonObject"/> payload using
    /// <see cref="JsonObjectExtensions.ReorderKeys"/>, stripping private keys if the requester is neither
    /// the user themselves nor authorized with global custom fields access.
    /// </summary>
    /// <param name="customFields">The raw custom fields JSON payload.</param>
    /// <param name="allOrderedKeys">All schema keys in display order.</param>
    /// <param name="publicOrderedKeys">Public schema keys in display order.</param>
    /// <param name="isSelf">Indicates whether the current requester is the target user.</param>
    /// <param name="hasGlobalAccess">Indicates whether the current requester has permission to view private custom fields.</param>
    /// <returns>A schema-ordered <see cref="JsonObject"/> containing only the fields visible to the requester.</returns>
    public static JsonObject ProtectCustomFields(
        this JsonObject? customFields,
        IReadOnlyList<string> allOrderedKeys,
        IReadOnlyList<string> publicOrderedKeys,
        bool isSelf,
        bool hasGlobalAccess)
        => (customFields ?? []).ReorderKeys(isSelf || hasGlobalAccess ? allOrderedKeys : publicOrderedKeys);

    /// <summary>
    /// Validates a partial <paramref name="incomingData"/> payload against the editable subset of <paramref name="schemaFields"/>
    /// and merges only the provided keys onto <paramref name="existingData"/>, preserving untouched editable values as well as
    /// protected private fields when the caller lacks private-field access.
    /// </summary>
    /// <param name="incomingData">The incoming partial JSON payload from the patch request.</param>
    /// <param name="existingData">The target user's currently persisted custom fields JSON payload loaded under row lock.</param>
    /// <param name="schemaFields">The ordered global user custom field definitions.</param>
    /// <param name="canEditPrivate">Whether the caller is editing their own profile or has <see cref="Domain.Enums.AccessRight.ViewCustomFields"/>.</param>
    /// <returns>A validated, merged, schema-ordered <see cref="JsonObject"/> ready to persist.</returns>
    public static JsonObject ValidateAndMergeCustomFields(
        this JsonObject incomingData,
        JsonObject? existingData,
        IReadOnlyList<UserCustomField> schemaFields,
        bool canEditPrivate)
    {
        if (canEditPrivate)
        {
            return incomingData.ValidateAndApplyPatch(existingData, schemaFields);
        }

        List<UserCustomField> publicFields = [.. schemaFields.Where(f => f.IsPublic)];
        JsonObject validatedPublic = incomingData.ValidateAndApplyPatch(existingData, publicFields);

        var merged = new JsonObject();
        foreach (var field in schemaFields)
        {
            string fieldKey = field.Id.ToString();
            if (field.IsPublic)
            {
                validatedPublic.Remove(fieldKey, out var publicNode);
                merged.Add(fieldKey, publicNode);
            }
            else if (existingData is not null && existingData.TryGetPropertyValue(fieldKey, out var existingNode))
            {
                merged.Add(fieldKey, existingNode?.DeepClone());
            }
            else
            {
                merged.Add(
                    fieldKey,
                    field.IsRequired ? JsonValue.Create(string.Empty) : null);
            }
        }

        return merged;
    }
}
