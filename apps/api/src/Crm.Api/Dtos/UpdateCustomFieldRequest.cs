using System.Text.Json.Serialization;
using Crm.Application.Dtos.User.Commands;

namespace Crm.Api.Dtos;

/// <summary>
/// Represents a request to update the value of an existing custom key-value field.
/// </summary>
/// <param name="Value">The new string value for the custom field.</param>
/// <param name="IsPublic">A value indicating whether the custom field is visible to other regular users.</param>
public record UpdateCustomFieldRequest(
    string Value,
    [property: JsonRequired] bool IsPublic)
{
    /// <summary>
    /// Maps the request payload to an <see cref="UpdateUserCustomFieldCommand"/> instance.
    /// </summary>
    /// <param name="customFieldId">The unique identifier of the custom field entry to update.</param>
    /// <param name="userId">The unique identifier of the user initiating the update.</param>
    public UpdateUserCustomFieldCommand ToCommand(Guid customFieldId, Guid userId) =>
        new(customFieldId, userId, Value, IsPublic);
}
