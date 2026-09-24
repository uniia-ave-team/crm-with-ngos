using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.Ngo.Commands;
/// <summary>
/// Represents a command to update an existing NGO in the system.
/// </summary>
/// <param name="Name">The new name for the NGO.</param>
/// <param name="LogoUrl">The new optional URL or path for the NGO's logo.</param>
public record UpdateNgoCommand(
    string Name,
    string? LogoUrl) : IRequest, ITransactionalCommand;
