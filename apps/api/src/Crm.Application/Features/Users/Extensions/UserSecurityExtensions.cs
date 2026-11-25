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
    /// Validates <paramref name="incomingData"/> against the editable subset of <paramref name="schemaFields"/>
    /// and merges the result with existing private field values when the caller lacks private-field access.
    /// Prevents unauthorized editors from overwriting or silently wiping private fields.
    /// </summary>
    /// <param name="incomingData">The incoming JSON payload from the update request.</param>
    /// <param name="existingData">The target user's currently persisted custom fields JSON payload.</param>
    /// <param name="schemaFields">The ordered global user custom field definitions.</param>
    /// <param name="canEditPrivate">Whether the caller is editing their own profile or has <see cref="Domain.Enums.AccessRight.ViewCustomFields"/>.</param>
    /// <returns>A validated, schema-ordered <see cref="JsonObject"/> ready to persist.</returns>
    public static JsonObject ValidateAndMergeCustomFields(
        this JsonObject incomingData,
        JsonObject? existingData,
        IReadOnlyList<UserCustomField> schemaFields,
        bool canEditPrivate)
    {
        if (canEditPrivate)
        {
            return incomingData.ValidateAndBackfillPayload(schemaFields);
        }

        List<UserCustomField> publicFields = [.. schemaFields.Where(f => f.IsPublic)];
        JsonObject validatedPublic = incomingData.ValidateAndBackfillPayload(publicFields);

        var merged = new JsonObject();
        foreach (var field in schemaFields)
        {
            if (field.IsPublic)
            {
                validatedPublic.Remove(field.Key, out var publicNode);
                merged.Add(field.Key, publicNode);
            }
            else if (existingData is not null && existingData.TryGetPropertyValue(field.Key, out var existingNode))
            {
                merged.Add(field.Key, existingNode?.DeepClone());
            }
            else
            {
                merged.Add(
                    field.Key,
                    field.IsRequired ? JsonNode.Parse(field.DataType.GetBackfillJson()) : null);
            }
        }

        return merged;
    }
}
