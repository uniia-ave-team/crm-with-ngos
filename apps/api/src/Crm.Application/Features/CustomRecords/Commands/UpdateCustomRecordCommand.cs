using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Represents a command to update an existing custom record's data payload.
/// </summary>
/// <param name="Data">The JSON object containing the updated field values for the custom record.</param>
/// <returns>Unit signaling command completion.</returns>
public record UpdateCustomRecordCommand(
    JsonObject Data) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the custom record to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}
