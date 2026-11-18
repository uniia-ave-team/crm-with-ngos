namespace Crm.Api.DemoMode;

/// <summary>
/// Determines the <see cref="DemoExecutionMode"/> for an incoming Mediator message when Demo Mode is active.
/// </summary>
public interface IDemoCommandPolicy
{
    /// <summary>
    /// Resolves the execution policy for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The Mediator request, command, or query being dispatched.</param>
    /// <returns>The resolved <see cref="DemoExecutionMode"/>.</returns>
    DemoExecutionMode ResolveMode(object message);
}
