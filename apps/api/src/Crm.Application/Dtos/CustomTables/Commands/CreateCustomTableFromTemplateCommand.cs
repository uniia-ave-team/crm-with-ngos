using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomTables.Commands;

/// <summary>
/// Represents a command to instantiate a new custom table from an existing template preset.
/// </summary>
/// <param name="SourceTemplateId">The unique identifier of the table template to instantiate from.</param>
/// <param name="Name">The display name of the newly instantiated table.</param>
/// <param name="Description">An optional description explaining the table's purpose.</param>
/// <returns>The unique identifier of the newly created custom table.</returns>
public record CreateCustomTableFromTemplateCommand(
    Guid SourceTemplateId,
    string Name,
    string? Description = null) : IRequest<Guid>, ITransactionalCommand;
