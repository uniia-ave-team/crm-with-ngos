using Crm.Application.Dtos.Roles;
using Mediator;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Represents a query to retrieve detailed information about a specific role by its ID.
/// </summary>
/// <param name="Id">The unique identifier of the role to retrieve.</param>
public record GetRoleByIdQuery(Guid Id) : IQuery<RoleDetailsDto>;
