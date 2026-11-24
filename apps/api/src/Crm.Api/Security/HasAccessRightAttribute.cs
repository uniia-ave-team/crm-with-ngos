using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Security;

/// <summary>
/// Specifies that the class or method requires a specific access right.
/// </summary>
/// <param name="right">The required access right to bind to the authorization policy.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasAccessRightAttribute(AccessRight right) : AuthorizeAttribute(right.ToClaimValue());
