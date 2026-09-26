using Crm.Domain.Entities;
using Crm.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Entities;

/// <summary>
/// Represents an authenticated system user handling security credentials, authentication state,
/// and session tokens. Inherits from ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/>
/// using a <see cref="Guid"/> identifier and maintains a 1-to-1 relationship with the business <see cref="User"/> profile.
/// </summary>
public class AuthUser : IdentityUser<Guid>, IEntity
{
    /// <summary>
    /// Gets or sets the extended business profile associated with this authentication user.
    /// </summary>
    public User UserProfile { get; set; }

    /// <summary>
    /// Gets or sets the collection of role assignments for this authentication user.
    /// </summary>
    public virtual ICollection<AuthUserRole> UserRoles { get; set; } = [];
}
