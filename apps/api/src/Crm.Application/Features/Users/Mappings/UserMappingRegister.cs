using Crm.Application.Features.Users.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Users.Mappings;

/// <summary>
/// Mapster configuration for User entity mappings.
/// Replaces manual Expression trees and manual ApplyUpdate methods.
/// </summary>
public sealed class UserMappingRegister : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<UpdateUserProfileCommand, User>();
    }
}
