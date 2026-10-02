using Crm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Security;

public class AccessRightRequirement(AccessRight requiredRight) : IAuthorizationRequirement
{
    public AccessRight RequiredRight { get; } = requiredRight;
}
