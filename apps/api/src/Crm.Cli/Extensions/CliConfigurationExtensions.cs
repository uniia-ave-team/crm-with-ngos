using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Crm.Cli.Extensions;

/// <summary>
/// Provides extension methods for configuring CLI application configuration providers.
/// </summary>
public static class CliConfigurationExtensions
{
    private const string AppSettingsFileName = "appsettings.json";
    private const string DotnetEnvironmentVar = "DOTNET_ENVIRONMENT";
    private const string AspnetCoreEnvironmentVar = "ASPNETCORE_ENVIRONMENT";
    private const string CliEnvironmentPrefix = "CRM_CLI_";

    /// <summary>
    /// Configures base settings for the CLI application: base path, appsettings.json, user secrets in Development,
    /// environment variables, and <c>CRM_CLI_</c>-prefixed environment variables that override everything else.
    /// </summary>
    /// <param name="builder">The configuration builder to configure.</param>
    /// <returns>The original configuration builder instance for method chaining.</returns>
    public static IConfigurationBuilder AddCliConfiguration(this IConfigurationBuilder builder)
    {
        var environment = Environment.GetEnvironmentVariable(DotnetEnvironmentVar)
            ?? Environment.GetEnvironmentVariable(AspnetCoreEnvironmentVar)
            ?? Environments.Production;

        builder
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(AppSettingsFileName, optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        if (string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly is not null)
            {
                builder.AddUserSecrets(entryAssembly, optional: true);
            }
        }

        builder.AddEnvironmentVariables();

        // The CLI runs inside the API container and shares its environment; prefixed variables apply to the CLI only.
        builder.AddEnvironmentVariables(CliEnvironmentPrefix);

        return builder;
    }
}
