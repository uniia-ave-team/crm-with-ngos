using Microsoft.AspNetCore.Identity;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents the join entity linking an authenticated system user (<see cref="AuthUser"/>)
/// with an application role (<see cref="AuthRole"/>) in a many-to-many relationship.
/// Inherits from ASP.NET Core Identity's <see cref="IdentityUserRole{TKey}"/> using a <see cref="Guid"/> identifier.
/// </summary>
public class AuthUserRole : IdentityUserRole<Guid>
{
    /// <summary>
    /// Gets or sets the associated authenticated user navigation property.
    /// </summary>
    public virtual AuthUser User { get; set; }

    /// <summary>
    /// Gets or sets the associated application role navigation property.
    /// </summary>
    public virtual AuthRole Role { get; set; }
}
