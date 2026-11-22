using Crm.Api.Options;
using Serilog;
using Serilog.Events;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring Serilog host logging and validating its options.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Binds and validates <see cref="SerilogOptions"/> from application configuration on startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddCustomSerilogOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SerilogOptions>()
            .Bind(configuration.GetSection(SerilogOptions.Position))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Configures Serilog as the primary logging provider, setting up console
    /// and file sinks with daily rolling intervals and log retention limits.
    /// </summary>
    /// <param name="host">The <see cref="ConfigureHostBuilder"/> to configure.</param>
    public static void AddCustomSerilog(this ConfigureHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);

        host.UseSerilog((ctx, lc) =>
        {
            var serilogOptions = new SerilogOptions();
            ctx.Configuration.GetSection(SerilogOptions.Position).Bind(serilogOptions);

            string logsPath = Path.IsPathRooted(serilogOptions.LogPath)
                ? serilogOptions.LogPath
                : Path.Combine(AppContext.BaseDirectory, serilogOptions.LogPath);

            lc.ReadFrom.Configuration(ctx.Configuration)
              .WriteTo.Console()
              .WriteTo.File(
                  Path.Combine(logsPath, serilogOptions.AllLogsFileName),
                  rollingInterval: RollingInterval.Day,
                  shared: true,
                  retainedFileCountLimit: serilogOptions.RetainedFileCountLimit)
              .WriteTo.File(
                  Path.Combine(logsPath, serilogOptions.ErrorLogsFileName),
                  rollingInterval: RollingInterval.Day,
                  shared: true,
                  restrictedToMinimumLevel: LogEventLevel.Error,
                  retainedFileCountLimit: serilogOptions.RetainedErrorFileCountLimit);
        });
    }
}
