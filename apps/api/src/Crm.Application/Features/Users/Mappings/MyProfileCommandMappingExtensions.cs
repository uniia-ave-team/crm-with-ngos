using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;

namespace Crm.Application.Features.Users.Mappings;

public static class MyProfileCommandMappingExtensions
{
    /// <summary>
    /// Maps UpdateProfileCommandDto to UpdateUserProfileCommand by supplying the specific user ID.
    /// </summary>
    public static UpdateUserProfileCommand ToUpdateUserProfileCommand(
        this UpdateProfileRequest command,
        Guid userId)
    {
        return new UpdateUserProfileCommand(
            UserId: userId,
            FirstName: command.FirstName,
            LastName: command.LastName,
            Patronymic: command.Patronymic,
            InternalPosition: command.InternalPosition,
            PhoneNumber: command.PhoneNumber,
            Country: command.Country,
            EmergencyContact: command.EmergencyContact,
            PreferredLanguage: command.PreferredLanguage);
    }

    /// <summary>
    /// Maps UpdatePasswordCommandDto to UpdatePasswordCommand by supplying the specific user ID.
    /// </summary>
    public static UpdatePasswordCommand ToUpdatePasswordCommand(
        this UpdatePasswordRequest command,
        Guid userId)
    {
        return new UpdatePasswordCommand(
            UserId: userId,
            CurrentPassword: command.CurrentPassword,
            NewPassword: command.NewPassword);
    }
}
