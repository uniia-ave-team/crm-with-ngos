using Crm.Application.Dtos.User;
using Crm.Infrastructure.Entities;
using Mapster;

namespace Crm.Infrastructure.Mappings;

/// <summary>
/// Mapster configuration for Infrastructure-specific entities (like Identity users).
/// </summary>
public class AuthUserMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AuthUser, UserDto>()
            .Map(dest => dest.FirstName, src => src.UserProfile.FirstName)
            .Map(dest => dest.LastName, src => src.UserProfile.LastName);

        config.NewConfig<AuthUser, UserStatusDto>()
            .Map(dest => dest.IsActive, src => src.UserProfile.IsActive);

        config.NewConfig<AuthUser, UserProfileResult>()
            .Map(dest => dest.FirstName, src => src.UserProfile.FirstName)
            .Map(dest => dest.LastName, src => src.UserProfile.LastName)
            .Map(dest => dest.Patronymic, src => src.UserProfile.Patronymic)
            .Map(dest => dest.InternalPosition, src => src.UserProfile.InternalPosition)
            .Map(dest => dest.PhoneNumber, src => src.UserProfile.PhoneNumber)
            .Map(dest => dest.Country, src => src.UserProfile.Country)
            .Map(dest => dest.EmergencyContact, src => src.UserProfile.EmergencyContact)
            .Map(dest => dest.PreferredLanguage, src => src.UserProfile.PreferredLanguage)
            .Map(dest => dest.Pronouns, src => src.UserProfile.Pronouns)
            .Map(dest => dest.PronounCategory, src => src.UserProfile.PronounCategory)
            .Map(dest => dest.AvatarUrl, src => src.UserProfile.AvatarUrl)
            .Map(dest => dest.CustomFields, src => src.UserProfile.CustomFields)
            .Ignore(dest => dest.Roles);
    }
}
