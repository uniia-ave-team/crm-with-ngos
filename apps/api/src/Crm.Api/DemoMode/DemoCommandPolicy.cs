using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Default <see cref="IDemoCommandPolicy"/> separating read-only queries, in-memory authentication session commands,
/// explicitly allowed commands (<see cref="IDemoAllowed"/> / <see cref="DemoAllowAttribute"/>), and dry-run transactional writes.
/// </summary>
/// <param name="demoModeOptions">Optional Demo Mode configuration options.</param>
/// <param name="httpContextAccessor">Optional HTTP context accessor used for configured excluded-path checks.</param>
public sealed class DemoCommandPolicy(
    IOptions<DemoModeOptions>? demoModeOptions = null,
    IHttpContextAccessor? httpContextAccessor = null) : IDemoCommandPolicy
{
    /// <inheritdoc />
    public DemoExecutionMode ResolveMode(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message is not ITransactionalCommand)
        {
            return DemoExecutionMode.None;
        }

        if (message is IDemoAllowed ||
            Attribute.IsDefined(message.GetType(), typeof(DemoAllowAttribute), inherit: true))
        {
            return DemoExecutionMode.ExplicitlyAllowed;
        }

        if (message is AuthenticateUserCommand or RefreshTokensCommand or LogoutCommand)
        {
            return DemoExecutionMode.SessionAuth;
        }

        var path = httpContextAccessor?.HttpContext?.Request.Path;
        return path.HasValue && DemoModeMiddleware.IsMatchingPath(path.Value, demoModeOptions?.Value.ExcludedPaths)
            ? DemoExecutionMode.ExplicitlyAllowed
            : DemoExecutionMode.DryRun;
    }
}
