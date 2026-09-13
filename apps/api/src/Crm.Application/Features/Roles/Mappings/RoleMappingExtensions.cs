using System.Linq.Expressions;
using Crm.Application.Dtos.Role;
using Crm.Domain.Entities;

namespace Crm.Application.Features.Roles.Mappings;

public static class RoleMappingExtensions
{
    /// <summary>
    /// Provides an expression to project an AuthRole entity to RoleDto directly in SQL.
    /// </summary>
    public static Expression<Func<AuthRole, RoleDto>> ToDtoExpression() => role => new(
        role.Id,
        role.Name!);

    /// <summary>
    /// Maps AuthRole domain entity to RoleDto.
    /// </summary>
    public static RoleDto ToDto(this AuthRole role) => new(
        role.Id,
        role.Name!);

    /// <summary>
    /// Maps AuthRole domain entity and its claims to RoleDetailsDto (for single item view).
    /// </summary>
    public static RoleDetailsDto ToDetailsDto(this AuthRole role, IEnumerable<string> claims) => new(
        role.Id,
        role.Name!,
        claims);
}
