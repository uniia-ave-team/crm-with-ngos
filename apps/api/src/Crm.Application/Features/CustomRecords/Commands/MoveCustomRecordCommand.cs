using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Represents a command to reposition an existing custom record inside its table's manual doubly-linked order
/// (for drag-and-drop reordering). When both neighbor identifiers are <see langword="null"/>, the record is moved to the end of the table.
/// </summary>
/// <param name="PreviousRecordId">Optional identifier of the record that should immediately precede the moved record.</param>
/// <param name="NextRecordId">Optional identifier of the record that should immediately follow the moved record.</param>
public record MoveCustomRecordCommand(
    Guid? PreviousRecordId = null,
    Guid? NextRecordId = null) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the custom record to move, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}
