namespace Crm.Api.Dtos;

/// <summary>
/// Represents a request to add a new custom key-value field to a user profile.
/// </summary>
/// <param name="Key">The unique key or name of the custom field.</param>
/// <param name="Value">The string value of the custom field.</param>
/// <param name="IsPublic">A value indicating whether the custom field is visible to other regular users.</param>
public record AddCustomFieldRequest(
    string Key,
    string Value,
    bool IsPublic);
