using System.Reflection;
using Crm.Cli.Abstractions;
using Crm.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Crm.Cli.Extensions;

/// <summary>
/// Provides extension methods for registering CLI routing components and application dependencies into the dependency injection container.
/// </summary>
public static class CliServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core services required by the CLI application, including persistence, repositories, identity, and commands.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration provider.</param>
    /// <returns>The original service collection instance for method chaining.</returns>
    public static IServiceCollection AddCliServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddRepositories();
        services.AddCustomIdentity(configuration);
        services.AddCliLogging();
        services.AddCliCommands();

        return services;
    }

    /// <summary>
    /// Configures console logging tailored for CLI execution, enabling single-line colorized output
    /// and routing log entries at or above <see cref="LogLevel.Error"/> to the standard error stream.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The original service collection instance for method chaining.</returns>
    public static IServiceCollection AddCliLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();

            builder.AddSimpleConsole(options =>
            {
                options.ColorBehavior = LoggerColorBehavior.Enabled;
                options.SingleLine = true;
                options.IncludeScopes = false;
                options.TimestampFormat = null;
            });

            builder.Services.Configure<ConsoleLoggerOptions>(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Error;
            });
        });

        return services;
    }

    /// <summary>
    /// Scans the executing assembly for all concrete implementations of <see cref="ICliCommand"/> and registers them as scoped services.
    /// Also registers the <see cref="CliRunner"/> dispatcher.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="commandAssembly">Optional assembly to scan for commands. Defaults to the assembly containing <see cref="CliRunner"/>.</param>
    /// <returns>The original service collection instance for method chaining.</returns>
    private static IServiceCollection AddCliCommands(this IServiceCollection services, Assembly? commandAssembly = null)
    {
        services.AddScoped<CliRunner>();

        var targetAssembly = commandAssembly ?? typeof(CliRunner).Assembly;

        var commandTypes = targetAssembly.GetTypes()
            .Where(t => typeof(ICliCommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in commandTypes)
        {
            services.AddScoped(typeof(ICliCommand), type);
        }

        return services;
    }
}
