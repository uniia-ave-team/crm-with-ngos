using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Users.Mappings;

/// <summary>
/// Mapster configuration for User, UserCustomField, and UserActionToken entity, DTO, and command mappings.
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

        config.NewConfig<UserActionToken, ActionTokenRecord>()
            .MapWith(src => new ActionTokenRecord(
                src.Id,
                src.Type,
                src.TargetEmail,
                src.EncryptedMetadata,
                src.CreatedAt,
                src.ExpiresAt,
                src.CreatedBy));

        config.NewConfig<UserActionToken, UserActionTokenRecord>()
            .MapWith(src => new UserActionTokenRecord(
                src.Id,
                src.Type,
                src.TargetEmail,
                src.TargetUserId,
                src.TargetUser != null ? src.TargetUser.FirstName : null,
                src.TargetUser != null ? src.TargetUser.LastName : null,
                src.TargetUser != null ? src.TargetUser.Avatar : null,
                src.EncryptedMetadata,
                src.CreatedAt,
                src.ExpiresAt,
                src.CreatedBy));

        config.NewConfig<UserActionToken, ActivePasswordResetTokenDto>()
            .MapWith(src => new ActivePasswordResetTokenDto(
                src.Id,
                src.TargetUserId ?? Guid.Empty,
                src.TargetEmail,
                src.TargetUser != null ? src.TargetUser.FirstName : string.Empty,
                src.TargetUser != null ? src.TargetUser.LastName : string.Empty,
                src.TargetUser != null ? src.TargetUser.Avatar : null,
                src.CreatedAt,
                src.ExpiresAt,
                src.CreatedBy));
    }
}
