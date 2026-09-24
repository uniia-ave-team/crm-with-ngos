using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Roles.Mappings;

/// <summary>
/// Mapster configuration for AuthRole entity mappings.
/// </summary>
public class RoleMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AuthRole, RoleDto>()
            .Map(dest => dest.Name, src => src.Name ?? string.Empty);

        config.NewConfig<AuthRole, RoleDetailsDto>()
            .Map(dest => dest.Name, src => src.Name ?? string.Empty)

            // Claims are loaded separately via RoleManager.GetClaimsAsync and set via `with` expression.
            .Ignore(dest => dest.Claims);

        config.NewConfig<UpdateRoleCommand, AuthRole>()
            .Map(dest => dest.Name, src => src.NewName)
            .Ignore(dest => dest.Id)
            .IgnoreNullValues(true);
    }
}
