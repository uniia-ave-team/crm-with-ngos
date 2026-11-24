using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.TableTemplates;
using Mediator;

namespace Crm.Application.Features.TableTemplates.Commands;

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
    List<CreateTemplateFieldDto> Fields) : ICommand<Guid>, ITransactionalCommand;
