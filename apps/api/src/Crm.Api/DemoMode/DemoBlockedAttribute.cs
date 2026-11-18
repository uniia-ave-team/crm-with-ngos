namespace Crm.Api.DemoMode;

/// <summary>
/// Marks a controller or action as blocked for write operations (<c>POST</c>, <c>PUT</c>, <c>PATCH</c>, <c>DELETE</c>)
/// when Demo Mode is active, returning <c>403 Forbidden</c> with error code <c>DEMO_MODE_RESTRICTED</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DemoBlockedAttribute : Attribute
{
}
