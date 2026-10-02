using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Security;

/// <summary>
/// Specifies that the class or method requires a specific access right.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class HasAccessRightAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HasAccessRightAttribute"/> class.
    /// </summary>
    /// <param name="right">The required access right to bind to the authorization policy.</param>
    public HasAccessRightAttribute(AccessRight right)
    {
        Policy = right.ToClaimValue();
    }
}
