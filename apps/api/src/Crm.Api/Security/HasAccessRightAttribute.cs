using Crm.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Security;

/// <summary>
/// Specifies that the class or method requires a specific access right.
/// </summary>
/// <param name="right">The required access right enum value.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class HasAccessRightAttribute(AccessRight right) : TypeFilterAttribute(typeof(AccessRightAuthorizationFilter))
{
    /// <summary>
    /// Gets the required access right value.
    /// </summary>
    public AccessRight RequiredRight { get; } = right;
}
