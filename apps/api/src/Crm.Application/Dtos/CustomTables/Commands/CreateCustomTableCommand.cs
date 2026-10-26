using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomTables.Commands;

/// <summary>
/// Represents a command to create a custom table from scratch without using a template.
/// </summary>
/// <param name="Name">The display name of the table.</param>
/// <param name="Description">An optional description for the table.</param>
/// <param name="Fields">Initial collection of custom field definitions.</param>
/// <returns>The unique identifier of the newly created custom table.</returns>
public record CreateCustomTableCommand(
    string Name,
    string? Description,
    List<CreateCustomFieldDto> Fields) : IRequest<Guid>, ITransactionalCommand;
