using System.Linq.Expressions;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Entities;

namespace Crm.Application.Features.Users.Mappings;

public static class UserMappingExtensions
{
    /// <summary>
    /// Provides an expression to project a User entity to UserDto directly in SQL.
    /// </summary>
    public static Expression<Func<User, UserDto>> ToDtoExpression() => u => new(
        u.Id,
        u.AuthUser.Email ?? string.Empty,
        u.FirstName,
        u.LastName);

    /// <summary>
    /// Provides an expression to project a User entity to UserProfileDto directly in SQL,
    /// including mapping its related roles.
    /// </summary>
    public static Expression<Func<User, UserProfileDto>> ToUserProfileDtoExpression(IEnumerable<RoleDto> roles) => u => new(
        u.Id,
        u.AuthUser.Email ?? string.Empty,
        u.FirstName,
        u.LastName,
        u.Patronymic,
        u.InternalPosition,
        u.PhoneNumber,
        u.Country,
        u.EmergencyContact,
        u.PreferredLanguage,
        roles);

    /// <summary>
    /// Applies the update command data to the existing User entity.
    /// </summary>
    public static void ApplyUpdate(this User user, UpdateUserProfileCommand request)
    {
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Patronymic = request.Patronymic;
        user.InternalPosition = request.InternalPosition;
        user.PhoneNumber = request.PhoneNumber;
        user.Country = request.Country;
        user.EmergencyContact = request.EmergencyContact;
        user.PreferredLanguage = request.PreferredLanguage;
    }
}
