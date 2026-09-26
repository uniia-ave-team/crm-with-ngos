using Crm.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Entities;

/// <summary>
/// Represents an application role used for role-based access control and authorization.
/// Inherits from ASP.NET Core Identity's <see cref="IdentityRole{TKey}"/> using a <see cref="Guid"/> identifier
/// and implements the domain-specific <see cref="IEntity"/> marker interface.
/// </summary>
public class AuthRole : IdentityRole<Guid>, IEntity
{
    /// <summary>
    /// Gets or sets the optional feminitive form of the role name.
    /// </summary>
    public string? FeminitiveName { get; set; }

    /// <summary>
    /// Gets or sets the optional plural form of the role name.
    /// </summary>
    public string? PluralName { get; set; }

    /// <summary>
    /// Gets or sets the collection of user role assignments associated with this role.
    /// </summary>
    public virtual ICollection<AuthUserRole> UserRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of claims (permissions) associated with this role.
    /// </summary>
    public virtual ICollection<AuthRoleClaim> RoleClaims { get; set; } = [];
}
