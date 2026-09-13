using Crm.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents an application role used for role-based access control and authorization.
/// Inherits from ASP.NET Core Identity's <see cref="IdentityRole{TKey}"/> using a <see cref="Guid"/> identifier
/// and implements the domain-specific <see cref="IEntity"/> marker interface.
/// </summary>
public class AuthRole : IdentityRole<Guid>, IEntity
{
}
