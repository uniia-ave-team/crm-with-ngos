using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.Ngo.Commands;
/// <summary>
/// Represents a command to create a new NGO in the system.
/// </summary>
/// <param name="Name">The name of the NGO to be created.</param>
public record CreateNgoCommand(string Name) : IRequest<Guid>, ITransactionalCommand;
