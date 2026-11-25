using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Users.Mappings;

/// <summary>
/// Mapster configuration for User and UserCustomField entity, DTO, and command mappings.
/// </summary>
public sealed class UserMappingRegister : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<UpdateUserProfileCommand, User>();

        config.NewConfig<UpdateUserCustomFieldDto, UserCustomField>()
            .Ignore(dest => dest.Id);

        config.NewConfig<UserCustomField, UserCustomFieldDto>();
        config.NewConfig<UserProfileResult, UserProfileDto>()
            .MapWith(src => new UserProfileDto(
                src.Id,
                src.Email,
                src.FirstName,
                src.LastName,
                src.Patronymic,
                src.PhoneNumber,
                src.Country,
                src.EmergencyContact,
                src.PreferredLanguage,
                src.Pronouns,
                src.PronounCategory,
                src.AvatarUrl,
                src.IsActive,
                src.Roles,
                src.CustomFields));
    }
}
