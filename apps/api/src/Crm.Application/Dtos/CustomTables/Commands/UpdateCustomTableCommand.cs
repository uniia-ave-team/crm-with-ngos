using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomTables.Commands;

/// <summary>
/// Represents a command to update the metadata (Name and Description) and field schema definitions of a custom table.
/// </summary>
/// <param name="Id">The unique identifier of the custom table to update.</param>
/// <param name="Name">The updated display name of the table.</param>
/// <param name="Description">The updated description for the table.</param>
/// <param name="Fields">The collection of field definitions to synchronize with the custom table schema.</param>
/// <returns>Unit signaling command completion.</returns>
public record UpdateCustomTableCommand(
    Guid Id,
    string Name,
    string? Description,
    List<UpdateCustomFieldDto> Fields) : IRequest, ITransactionalCommand;
