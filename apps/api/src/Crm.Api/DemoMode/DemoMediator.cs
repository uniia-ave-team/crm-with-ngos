using Crm.Domain.Exceptions;
using Mediator;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Decorator around <see cref="IMediator"/> that enforces Demo Mode execution scopes
/// via <see cref="IDemoCommandPolicy"/> and extensible <see cref="IDemoCommandInterceptor"/> hooks.
/// </summary>
public sealed class DemoMediator : IMediator
{
    private readonly IMediator _inner;
    private readonly IDemoModeState _demoModeState;
    private readonly IDemoExecutionContext _executionContext;
    private readonly IDemoCommandPolicy _commandPolicy;
    private readonly IReadOnlyList<IDemoCommandInterceptor> _interceptors;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoMediator"/> class with explicit policy and interceptors.
    /// </summary>
    /// <param name="inner">The underlying <see cref="IMediator"/> implementation.</param>
    /// <param name="demoModeState">The service providing current Demo Mode state.</param>
    /// <param name="executionContext">The async-local execution scope tracker for Demo Mode.</param>
    /// <param name="commandPolicy">The policy resolving <see cref="DemoExecutionMode"/> per message.</param>
    /// <param name="interceptors">Extensible interceptors invoked around dry-run command execution.</param>
    public DemoMediator(
        IMediator inner,
        IDemoModeState demoModeState,
        IDemoExecutionContext executionContext,
        IDemoCommandPolicy commandPolicy,
        IEnumerable<IDemoCommandInterceptor> interceptors)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(demoModeState);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(commandPolicy);
        ArgumentNullException.ThrowIfNull(interceptors);

        _inner = inner;
        _demoModeState = demoModeState;
        _executionContext = executionContext;
        _commandPolicy = commandPolicy;
        _interceptors = [.. interceptors];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoMediator"/> class with default policy and dry-run follow-up interceptor.
    /// </summary>
    /// <param name="inner">The underlying <see cref="IMediator"/> implementation.</param>
    /// <param name="demoModeState">The service providing current Demo Mode state.</param>
    /// <param name="executionContext">The async-local execution scope tracker for Demo Mode.</param>
    /// <param name="demoModeOptions">Optional options providing <see cref="DemoModeOptions.ExcludedPaths"/>.</param>
    /// <param name="httpContextAccessor">Optional HTTP context accessor used for excluded path matching.</param>
    public DemoMediator(
        IMediator inner,
        IDemoModeState demoModeState,
        IDemoExecutionContext executionContext,
        IOptions<DemoModeOptions>? demoModeOptions = null,
        IHttpContextAccessor? httpContextAccessor = null)
        : this(
            inner,
            demoModeState,
            executionContext,
            new DemoCommandPolicy(demoModeOptions, httpContextAccessor),
            [new DemoDryRunFollowUpInterceptor(new DemoDryRunIdRegistry(TimeProvider.System))])
    {
    }

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => !_demoModeState.IsActive
            ? _inner.Send(request, cancellationToken)
            : ExecuteWithDemoScopeAsync(request, () => _inner.Send(request, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        => !_demoModeState.IsActive
            ? _inner.Send(command, cancellationToken)
            : ExecuteWithDemoScopeAsync(command, () => _inner.Send(command, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        => !_demoModeState.IsActive
            ? _inner.Send(query, cancellationToken)
            : ExecuteWithDemoScopeAsync(query, () => _inner.Send(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return !_demoModeState.IsActive
            ? _inner.Send(message, cancellationToken)
            : SendObjectWithDemoScopeAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamQuery<TResponse> query,
        CancellationToken cancellationToken = default)
        => _inner.CreateStream(query, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
        => _inner.CreateStream(request, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamCommand<TResponse> command,
        CancellationToken cancellationToken = default)
        => _inner.CreateStream(command, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<object?> CreateStream(
        object message,
        CancellationToken cancellationToken = default)
        => _inner.CreateStream(message, cancellationToken);

    /// <inheritdoc />
    public ValueTask Publish<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
        => !_demoModeState.IsActive
            ? _inner.Publish(notification, cancellationToken)
            : PublishWithDemoScopeAsync(() => _inner.Publish(notification, cancellationToken));

    /// <inheritdoc />
    public ValueTask Publish(
        object notification,
        CancellationToken cancellationToken = default)
        => !_demoModeState.IsActive
            ? _inner.Publish(notification, cancellationToken)
            : PublishWithDemoScopeAsync(() => _inner.Publish(notification, cancellationToken));

    private async ValueTask PublishWithDemoScopeAsync(Func<ValueTask> publish)
    {
        var targetMode = _executionContext.IsExplicitlyAllowed
            ? DemoExecutionMode.ExplicitlyAllowed
            : DemoExecutionMode.DryRun;

        using var scope = _executionContext.EnterScope(targetMode);
        await publish();
    }

    private async ValueTask<object?> SendObjectWithDemoScopeAsync(object message, CancellationToken cancellationToken)
    {
        var mode = _commandPolicy.ResolveMode(message);
        using var scope = _executionContext.EnterScope(mode);

        foreach (var interceptor in _interceptors)
        {
            var (handled, interceptedResponse) = await interceptor.TryHandleAsync<object?>(message, mode, cancellationToken);
            if (handled)
            {
                if (mode == DemoExecutionMode.DryRun)
                {
                    NotifyDryRunSucceeded(message, interceptedResponse);
                }

                return interceptedResponse;
            }
        }

        try
        {
            var response = await _inner.Send(message, cancellationToken);
            if (mode == DemoExecutionMode.DryRun)
            {
                NotifyDryRunSucceeded(message, response);
            }

            return response;
        }
        catch (NotFoundException ex)
        {
            foreach (var interceptor in _interceptors)
            {
                var (recovered, fallback) = await interceptor.TryRecoverNotFoundAsync<Unit>(message, mode, ex, cancellationToken);
                if (recovered)
                {
                    return fallback;
                }
            }

            throw;
        }
    }

    private async ValueTask<TResponse> ExecuteWithDemoScopeAsync<TResponse>(
        object message,
        Func<ValueTask<TResponse>> execute,
        CancellationToken cancellationToken)
    {
        var mode = _commandPolicy.ResolveMode(message);
        using var scope = _executionContext.EnterScope(mode);

        foreach (var interceptor in _interceptors)
        {
            var (handled, interceptedResponse) = await interceptor.TryHandleAsync<TResponse>(message, mode, cancellationToken);
            if (handled)
            {
                if (mode == DemoExecutionMode.DryRun)
                {
                    NotifyDryRunSucceeded(message, interceptedResponse);
                }

                return interceptedResponse;
            }
        }

        try
        {
            var response = await execute();
            if (mode == DemoExecutionMode.DryRun)
            {
                NotifyDryRunSucceeded(message, response);
            }

            return response;
        }
        catch (NotFoundException ex)
        {
            foreach (var interceptor in _interceptors)
            {
                var (recovered, fallback) = await interceptor.TryRecoverNotFoundAsync<TResponse>(message, mode, ex, cancellationToken);
                if (recovered)
                {
                    return fallback;
                }
            }

            throw;
        }
    }

    private void NotifyDryRunSucceeded(object message, object? response)
    {
        foreach (var interceptor in _interceptors)
        {
            interceptor.OnDryRunSucceeded(message, response);
        }
    }
}
