using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.TableTemplates;
using Mediator;

namespace Crm.Application.Features.TableTemplates.Commands;

/// <summary>
/// Represents a command to update an existing table template and its field structure.
/// </summary>
/// <param name="Name">The updated display name of the template.</param>
/// <param name="Description">The updated description.</param>
/// <param name="Fields">The full updated list of field schema definitions.</param>
/// <returns>Unit signaling command completion.</returns>
public record UpdateTableTemplateCommand(
    string Name,
    string? Description,
    List<UpdateTemplateFieldDto> Fields) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the template to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}
