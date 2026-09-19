using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Users.Mappings;

/// <summary>
/// Mapster configuration for User entity mappings.
/// Replaces manual Expression trees and manual ApplyUpdate methods.
/// </summary>
public class UserMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<User, UserDto>()
            .Map(dest => dest.Email, src => src.AuthUser.Email ?? string.Empty);

        config.NewConfig<User, UserProfileDto>()
            .Map(dest => dest.Email, src => src.AuthUser.Email ?? string.Empty)
            .Ignore(dest => dest.Roles);

        config.NewConfig<UpdateUserProfileCommand, User>()
            .IgnoreNullValues(true)
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.AuthUser);
    }
}
