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
            .Map(dest => dest.Email, src => src.Email ?? string.Empty)
            .Map(dest => dest.FirstName, src => src.UserProfile.FirstName)
            .Map(dest => dest.LastName, src => src.UserProfile.LastName)
            .Map(dest => dest.Patronymic, src => src.UserProfile.Patronymic)
            .Map(dest => dest.PhoneNumber, src => src.UserProfile.PhoneNumber)
            .Map(dest => dest.Country, src => src.UserProfile.Country)
            .Map(dest => dest.EmergencyContact, src => src.UserProfile.EmergencyContact)
            .Map(dest => dest.Pronouns, src => src.UserProfile.Pronouns)
            .Map(dest => dest.PronounCategory, src => src.UserProfile.PronounCategory)
            .Map(dest => dest.AvatarUrl, src => src.UserProfile.Avatar)
            .Map(dest => dest.IsActive, src => src.UserProfile.IsActive)
            .Ignore(dest => dest.Roles);

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
                src.UserProfile.CustomFields
                    .OrderBy(cf => cf.Id)
                    .Select(cf => new UserCustomFieldDto(
                        cf.Id,
                        cf.Key,
                        cf.Value,
                        cf.IsPublic))
                    .ToList()));
    }
}
