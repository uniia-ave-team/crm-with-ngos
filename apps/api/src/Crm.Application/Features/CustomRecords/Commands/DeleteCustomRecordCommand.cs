using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Represents a command to delete a specific custom record by its identifier.
/// </summary>
/// <param name="Id">The unique identifier of the record to delete.</param>
/// <returns>Unit signaling command completion.</returns>
public record DeleteCustomRecordCommand(
    Guid Id) : ICommand, ITransactionalCommand, IHasEntityId;
