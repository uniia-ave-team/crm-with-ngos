using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Common;
using Mediator;

namespace Crm.Application.Features.TableTemplates.Commands;

/// <summary>
/// Represents a command to update an existing non-system table template and replace its field blueprint list.
/// </summary>
/// <param name="Name">The updated display name of the template.</param>
/// <param name="Description">The updated optional description of the template.</param>
/// <param name="Fields">The updated ordered list of field blueprint definitions.</param>
/// <returns>Unit signaling command completion.</returns>
public record UpdateTableTemplateCommand(
    string Name,
    string? Description,
    List<FieldDefinitionDto> Fields) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the table template to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}
