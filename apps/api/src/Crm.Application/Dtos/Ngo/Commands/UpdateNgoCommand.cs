using Crm.Application.Interfaces;
using Mediator;

namespace Crm.Application.Dtos.Ngo.Commands;
/// <summary>
/// Represents a command to update an existing NGO in the system.
/// </summary>
/// <param name="Name">The new name for the NGO.</param>
public record UpdateNgoCommand(string Name) : IRequest, ITransactionalCommand;
