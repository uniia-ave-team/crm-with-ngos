using System.Text.Json.Serialization;
using Crm.Application.Dtos.User.Commands;

namespace Crm.Api.Dtos;

/// <summary>
/// Represents a request to add a new custom key-value field to a user profile.
/// </summary>
/// <param name="Key">The unique key or name of the custom field.</param>
/// <param name="Value">The string value of the custom field.</param>
/// <param name="IsPublic">A value indicating whether the custom field is visible to other regular users.</param>
public record AddCustomUserFieldRequest(
    string Key,
    string Value,
    [property: JsonRequired] bool IsPublic)
{
    /// <summary>
    /// Maps the request payload to an <see cref="AddUserCustomFieldCommand"/> instance.
    /// </summary>
    /// <param name="userId">The unique identifier of the target user from route or context.</param>
    public AddUserCustomFieldCommand ToCommand(Guid userId) =>
        new(userId, Key, Value, IsPublic);
}
