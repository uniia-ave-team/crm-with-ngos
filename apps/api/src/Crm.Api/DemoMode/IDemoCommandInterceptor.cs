using Crm.Domain.Exceptions;

namespace Crm.Api.DemoMode;

/// <summary>
/// Extensible hook invoked by <see cref="DemoMediator"/> to safely intercept sensitive commands
/// (avoiding PostgreSQL row locks or real cryptographic token issuance), track dry-run created IDs,
/// or recover from expected dry-run <see cref="NotFoundException"/> errors.
/// </summary>
public interface IDemoCommandInterceptor
{
    /// <summary>
    /// Attempts to handle <paramref name="message"/> directly in Demo Mode without invoking the underlying handler.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="message">The command being dispatched.</param>
    /// <param name="mode">The active <see cref="DemoExecutionMode"/> for this command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating whether the command was handled and the resulting response.</returns>
    ValueTask<(bool Handled, TResponse Response)> TryHandleAsync<TResponse>(
        object message,
        DemoExecutionMode mode,
        CancellationToken cancellationToken)
        => ValueTask.FromResult((false, default(TResponse)!));

    /// <summary>
    /// Invoked after a dry-run command completes successfully, allowing tracking of newly created dry-run identifiers.
    /// </summary>
    /// <param name="message">The command that completed.</param>
    /// <param name="response">The response returned by the command.</param>
    void OnDryRunSucceeded(object message, object? response)
    {
    }

    /// <summary>
    /// Attempts to recover from a <see cref="NotFoundException"/> thrown during a dry-run follow-up command.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="message">The command that threw <paramref name="exception"/>.</param>
    /// <param name="mode">The active <see cref="DemoExecutionMode"/> for this command.</param>
    /// <param name="exception">The thrown <see cref="NotFoundException"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating whether recovery succeeded and the fallback response.</returns>
    ValueTask<(bool Handled, TResponse Response)> TryRecoverNotFoundAsync<TResponse>(
        object message,
        DemoExecutionMode mode,
        NotFoundException exception,
        CancellationToken cancellationToken)
        => ValueTask.FromResult((false, default(TResponse)!));
}
