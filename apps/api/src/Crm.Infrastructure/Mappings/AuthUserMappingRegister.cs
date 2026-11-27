using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Infrastructure.Entities;
using Mapster;

namespace Crm.Infrastructure.Mappings;

/// <summary>
/// Mapster configuration for Infrastructure-specific entities (like Identity users).
/// </summary>
public sealed class AuthUserMappingRegister : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AuthUser, UserDto>()
            .MapWith(src => new UserDto(
                src.Id,
                src.Email ?? string.Empty,
                src.UserProfile.FirstName,
                src.UserProfile.LastName,
                src.UserProfile.Patronymic,
                src.UserProfile.PhoneNumber,
                src.UserProfile.Country,
                src.UserProfile.EmergencyContact,
                src.UserProfile.Pronouns,
                src.UserProfile.PronounCategory,
                src.UserProfile.Avatar,
                src.UserProfile.IsActive,
                Array.Empty<RoleUserDto>(),
                src.UserProfile.CustomFields));

        config.NewConfig<AuthUser, UserStatusDto>()
            .Map(dest => dest.Email, src => src.Email ?? string.Empty)
            .Map(dest => dest.IsActive, src => src.UserProfile.IsActive);

        config.NewConfig<AuthUser, UserBasicDto>()
            .Map(dest => dest.Email, src => src.Email ?? string.Empty);

        config.NewConfig<AuthUser, UserProfileResult>()
            .MapWith(src => new UserProfileResult(
                src.Id,
                src.Email ?? string.Empty,
                src.UserProfile.FirstName,
                src.UserProfile.LastName,
                src.UserProfile.Patronymic,
                src.UserProfile.PhoneNumber,
                src.UserProfile.Country,
                src.UserProfile.EmergencyContact,
                src.UserProfile.PreferredLanguage,
                src.UserProfile.Pronouns,
                src.UserProfile.PronounCategory,
                src.UserProfile.Avatar,
                src.UserProfile.IsActive,
                Array.Empty<RoleUserDto>(),
                src.UserProfile.CustomFields));
    }
}
