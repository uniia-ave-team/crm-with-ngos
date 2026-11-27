using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Common;
using Mediator;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Represents a command to create a new custom table from scratch for the organization's NGO.
/// Field storage keys are generated automatically by the server.
/// </summary>
/// <param name="Name">The display name of the custom table.</param>
/// <param name="Description">An optional description explaining the table's purpose.</param>
/// <param name="Fields">The ordered list of field definitions making up the table schema.</param>
/// <returns>The unique identifier of the newly created custom table.</returns>
public record CreateCustomTableCommand(
    string Name,
    string? Description,
    List<FieldDefinitionDto> Fields) : ICommand<Guid>, ITransactionalCommand;
