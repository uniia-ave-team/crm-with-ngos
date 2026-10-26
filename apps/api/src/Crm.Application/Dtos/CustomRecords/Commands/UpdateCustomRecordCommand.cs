using System.Text.Json.Nodes;
using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomRecords.Commands;

/// <summary>
/// Represents a command to update an existing custom record's data payload.
/// </summary>
/// <param name="Id">The unique identifier of the custom record to update.</param>
/// <param name="Data">The JSON object containing the updated field values for the custom record.</param>
/// <returns>Unit signaling command completion.</returns>
public record UpdateCustomRecordCommand(
    Guid Id,
    JsonObject Data) : IRequest, ITransactionalCommand;
