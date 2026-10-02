using MediatR;

namespace Crm.Application.Dtos.Role.Queries;
/// <summary>
/// Represents a query to retrieve detailed information about a specific role by its ID.
/// </summary>
/// <param name="Id">The unique identifier of the role to retrieve.</param>
public record GetRoleByIdQuery(Guid Id) : IRequest<RoleDetailsDto>;
