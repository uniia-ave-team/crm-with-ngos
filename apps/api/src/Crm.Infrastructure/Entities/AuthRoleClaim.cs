using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Entities;

/// <summary>
/// Represents a specific claim (permission) assigned to an application role (<see cref="AuthRole"/>).
/// Inherits from ASP.NET Core Identity's <see cref="IdentityRoleClaim{TKey}"/> using a <see cref="Guid"/> identifier.
/// </summary>
public class AuthRoleClaim : IdentityRoleClaim<Guid>
{
    /// <summary>
    /// Gets or sets the associated application role navigation property.
    /// </summary>
    public virtual AuthRole Role { get; set; }
}
