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
        config.NewConfig<UpdateUserProfileCommand, User>()
            .IgnoreNullValues(true)
            .Map(dest => dest.Avatar, src => src.AvatarUrl)
            .Ignore(dest => dest.Id);
    }
}
