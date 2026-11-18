namespace Crm.Api.DemoMode;

/// <summary>
/// Marks a command class as allowed to commit database changes even when Demo Mode is active.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DemoAllowAttribute : Attribute
{
}
