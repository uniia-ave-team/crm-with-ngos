using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomTables.Commands;

/// <summary>
/// Represents a command to delete a custom table along with all its fields and records.
/// </summary>
/// <param name="Id">The unique identifier of the table to delete.</param>
/// <returns>Unit signaling command completion.</returns>
public record DeleteCustomTableCommand(
    Guid Id) : IRequest, ITransactionalCommand;
