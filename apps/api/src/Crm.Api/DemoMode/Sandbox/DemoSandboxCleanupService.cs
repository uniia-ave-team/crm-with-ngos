using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Periodically removes the sandboxes that outlived their idle timeout or maximum lifetime.
/// </summary>
/// <param name="sandboxManager">The sandbox manager.</param>
/// <param name="demoModeOptions">The demo mode options.</param>
/// <param name="timeProvider">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed partial class DemoSandboxCleanupService(
    DemoSandboxManager sandboxManager,
    IOptions<DemoModeOptions> demoModeOptions,
    TimeProvider timeProvider,
    ILogger<DemoSandboxCleanupService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(demoModeOptions.Value.Sandbox.CleanupIntervalSeconds);

        using var timer = new PeriodicTimer(interval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await sandboxManager.RemoveExpiredAsync(stoppingToken);
                }
#pragma warning disable CA1031 // One failed pass must not stop the cleanup loop.
                catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    LogCleanupFailed(logger, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The application is shutting down.
        }
    }

    [LoggerMessage(EventId = DemoLogEventIds.SandboxCleanupFailed, Level = LogLevel.Error, Message = "Demo sandbox cleanup failed.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
