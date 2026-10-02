using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.Ngo.Commands;
/// <summary>
/// Represents a command to create a new NGO in the system.
/// </summary>
/// <param name="Name">The name of the NGO to be created.</param>
/// <param name="LogoUrl">The optional URL or path for the NGO's logo.</param>
public record CreateNgoCommand(
    string Name,
    string? LogoUrl) : IRequest<Guid>, ITransactionalCommand;
