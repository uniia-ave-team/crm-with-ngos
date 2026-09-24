using MediatR;

namespace Crm.Application.Dtos.Role.Commands;

/// <summary>
/// Represents a command to create a new Role in the system.
/// </summary>
/// <param name="Name">The name of the role to be created.</param>
/// <param name="FeminitiveName">The optional feminitive form of the role name (e.g., "Адміністраторка").</param>
/// <param name="PluralName">The optional plural form of the role name (e.g., "Адміністратори").</param>
public record CreateRoleCommand(
    string Name,
    string? FeminitiveName,
    string? PluralName) : IRequest<Guid>;
