using System.Text.Json.Nodes;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Represents a command to create a new dynamic data record inside a custom table.
/// When both <paramref name="PreviousRecordId"/> and <paramref name="NextRecordId"/> are <see langword="null"/>,
/// the record is appended at the end (tail) of the table.
/// </summary>
/// <param name="CustomTableId">The unique identifier of the target custom table.</param>
/// <param name="Data">The JSON object containing the string field values for the new custom record.</param>
/// <param name="PreviousRecordId">Optional identifier of the preceding neighbor in manual row order.</param>
/// <param name="NextRecordId">Optional identifier of the following neighbor in manual row order.</param>
/// <returns>The unique identifier of the newly created custom record.</returns>
public record CreateCustomRecordCommand(
    Guid CustomTableId,
    JsonObject Data,
    Guid? PreviousRecordId = null,
    Guid? NextRecordId = null) : ICommand<Guid>, ITransactionalCommand;
