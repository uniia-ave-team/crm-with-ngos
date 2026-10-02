using Crm.Application.Dtos.Role;
using Crm.Domain.Enums;

namespace Crm.Application.Features.Roles.Extensions;

/// <summary>
/// Provides extension methods for role data transfer objects to handle localized and context-aware data.
/// </summary>
public static class RoleDtoExtensions
{
    /// <summary>
    /// Resolves the appropriate role name based on the user's grammatical gender (pronoun category)
    /// and projects it into a lightweight <see cref="RoleUserDto"/>.
    /// </summary>
    /// <param name="role">The role DTO to resolve the name for.</param>
    /// <param name="pronounCategory">The grammatical category of the user's pronouns.</param>
    /// <returns>A new <see cref="RoleUserDto"/> with the resolved name.</returns>
    public static RoleUserDto ResolveNameForUser(this RoleDto role, PronounCategory? pronounCategory)
    {
        var resolvedName = pronounCategory switch
        {
            PronounCategory.Feminine when !string.IsNullOrWhiteSpace(role.FeminitiveName) => role.FeminitiveName,
            PronounCategory.Neutral when !string.IsNullOrWhiteSpace(role.PluralName) => role.PluralName,
            PronounCategory.Masculine when !string.IsNullOrWhiteSpace(role.Name) => role.Name,
            null or _ => role.Name ?? string.Empty,
        };

        return new RoleUserDto(role.Id, resolvedName);
    }
}
