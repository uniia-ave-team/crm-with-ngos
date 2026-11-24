using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Represents a command to update an existing role's details.
/// </summary>
/// <param name="NewName">The new name to assign to the role.</param>
/// <param name="FeminitiveName">The optional feminitive form of the role name.</param>
/// <param name="PluralName">The optional plural form of the role name.</param>
public record UpdateRoleCommand(
    string NewName,
    string? FeminitiveName,
    string? PluralName) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the role to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}
