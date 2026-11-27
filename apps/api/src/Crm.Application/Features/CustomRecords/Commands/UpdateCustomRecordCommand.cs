using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Represents a command to partially update an existing custom record's data payload.
/// Only the keys present in <paramref name="Data"/> are merged onto the latest persisted payload; omitted keys remain untouched.
/// </summary>
/// <param name="Data">The partial JSON object containing changed field values keyed by field ID.</param>
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
