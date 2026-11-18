namespace Crm.Api.DemoMode;

/// <summary>
/// Marker interface indicating that a command is permitted to commit database changes
/// even when Demo Mode is active (for example, authentication and token rotation commands).
/// </summary>
public interface IDemoAllowed
{
}
