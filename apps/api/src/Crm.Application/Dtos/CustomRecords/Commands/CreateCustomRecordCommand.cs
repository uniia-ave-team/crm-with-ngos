using System.Text.Json.Nodes;
using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomRecords.Commands;

/// <summary>
/// Represents a command to create a new dynamic data record inside a custom table.
/// </summary>
/// <param name="CustomTableId">The unique identifier of the target custom table.</param>
/// <param name="Data">The JSON object containing the field values for the new custom record.</param>
/// <returns>The unique identifier of the newly created custom record.</returns>
public record CreateCustomRecordCommand(
    Guid CustomTableId,
    JsonObject Data) : IRequest<Guid>, ITransactionalCommand;
