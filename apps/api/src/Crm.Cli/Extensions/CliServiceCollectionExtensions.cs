using System.Reflection;
using Crm.Cli.Abstractions;
using Crm.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddCliLogging(configuration);
        services.AddCliCommands();

        return services;
    }

    /// <summary>
    /// Configures console logging for CLI execution. Levels, the error stream threshold and formatter options
    /// come from the <c>Logging</c> configuration section, so they can be overridden with environment variables.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration provider.</param>
    /// <returns>The original service collection instance for method chaining.</returns>
    public static IServiceCollection AddCliLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddSimpleConsole();
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
