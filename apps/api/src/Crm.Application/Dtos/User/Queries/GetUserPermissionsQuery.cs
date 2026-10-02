using MediatR;

namespace Crm.Application.Dtos.User.Queries;

/// <summary>
/// Query to fetch all unique UI permissions for a specific user roles.
/// </summary>
public record GetUserPermissionsQuery(List<Guid> RoleIds) : IRequest<List<string>>;
