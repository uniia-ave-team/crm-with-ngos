using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Ngos.Commands;

/// <summary>
/// Represents a command to create a new NGO in the system.
/// </summary>
/// <param name="Name">The name of the NGO to be created.</param>
public record CreateNgoCommand(string Name) : ICommand<Guid>, ITransactionalCommand;
