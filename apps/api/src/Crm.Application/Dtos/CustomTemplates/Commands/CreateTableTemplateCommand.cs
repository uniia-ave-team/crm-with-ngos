using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomTemplates.Commands;

/// <summary>
/// Represents a command to create a reusable table template blueprint.
/// </summary>
/// <param name="Name">The display name of the template preset.</param>
/// <param name="Description">An optional explanation of intended use cases.</param>
/// <param name="Fields">Collection of predefined field schema definitions.</param>
/// <returns>The unique identifier of the newly created template.</returns>
public record CreateTableTemplateCommand(
    string Name,
    string? Description,
    List<CreateTemplateFieldDto> Fields) : IRequest<Guid>, ITransactionalCommand;
