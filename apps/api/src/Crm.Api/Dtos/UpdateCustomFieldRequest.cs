namespace Crm.Api.Dtos;

/// <summary>
/// Represents a request to update the value of an existing custom key-value field.
/// </summary>
/// <param name="Value">The new string value for the custom field.</param>
public record UpdateCustomFieldRequest(
    string Value);
