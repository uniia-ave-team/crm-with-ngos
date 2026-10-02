namespace Crm.Application.Dtos.User;

/// <summary>
/// Represents a custom key-value pair field associated with a user.
/// </summary>
/// <param name="Id">The unique identifier of the custom field entry.</param>
/// <param name="Key">The unique key or name of the custom field.</param>
/// <param name="Value">The string value of the custom field.</param>
/// <param name="IsPublic">A value indicating whether the custom field is visible to other regular users.</param>
public record UserCustomFieldDto(
    Guid Id,
    string Key,
    string Value,
    bool IsPublic);
