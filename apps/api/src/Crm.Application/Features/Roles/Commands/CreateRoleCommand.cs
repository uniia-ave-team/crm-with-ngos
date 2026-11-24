using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Represents a command to create a new Role in the system.
/// </summary>
/// <param name="Name">The name of the role to be created.</param>
/// <param name="FeminitiveName">The optional feminitive form of the role name.</param>
/// <param name="PluralName">The optional plural form of the role name.</param>
public record CreateRoleCommand(
    string Name,
    string? FeminitiveName,
    string? PluralName) : ICommand<Guid>, ITransactionalCommand;
