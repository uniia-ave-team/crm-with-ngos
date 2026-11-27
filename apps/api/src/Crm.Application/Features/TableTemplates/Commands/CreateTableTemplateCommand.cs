using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Common;
using Mediator;

namespace Crm.Application.Features.TableTemplates.Commands;

/// <summary>
/// Represents a command to create a new reusable table template blueprint.
/// </summary>
/// <param name="Name">The display name of the template.</param>
/// <param name="Description">An optional description explaining the template's purpose.</param>
/// <param name="Fields">The ordered list of field blueprint definitions.</param>
/// <returns>The unique identifier of the newly created table template.</returns>
public record CreateTableTemplateCommand(
    string Name,
    string? Description,
    List<FieldDefinitionDto> Fields) : ICommand<Guid>, ITransactionalCommand;
