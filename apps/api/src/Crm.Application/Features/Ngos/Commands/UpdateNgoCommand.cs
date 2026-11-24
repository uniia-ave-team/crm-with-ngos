using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Ngos.Commands;

/// <summary>
/// Represents a command to update an existing NGO in the system.
/// </summary>
/// <param name="Name">The new name for the NGO.</param>
public record UpdateNgoCommand(string Name) : ICommand, ITransactionalCommand;
