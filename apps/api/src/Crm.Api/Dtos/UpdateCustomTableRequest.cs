using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;

namespace Crm.Api.Dtos;

/// <summary>
/// Represents the request payload for updating a custom table.
/// </summary>
/// <param name="Name">The updated display name of the custom table.</param>
/// <param name="Description">The updated optional description for the custom table.</param>
/// <param name="Fields">The updated list of fields for the custom table.</param>
public record UpdateCustomTableRequest(
    string Name,
    string? Description,
    List<UpdateCustomFieldDto> Fields)
{
    /// <summary>
    /// Maps the request payload to an <see cref="UpdateCustomTableCommand"/> instance.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table from the route.</param>
    public UpdateCustomTableCommand ToCommand(Guid id) =>
        new(id, Name, Description, Fields);
}
