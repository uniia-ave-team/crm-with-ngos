using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.CustomTables;
using Mediator;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Represents a command to update the metadata (Name and Description) and field schema definitions of a custom table.
/// </summary>
/// <param name="Name">The updated display name of the table.</param>
/// <param name="Description">The updated description for the table.</param>
/// <param name="Fields">The collection of field definitions to synchronize with the custom table schema.</param>
/// <returns>Unit signaling command completion.</returns>
public record UpdateCustomTableCommand(
    string Name,
    string? Description,
    List<UpdateCustomFieldDto> Fields) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the custom table to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}
