using System.Reflection;
using Crm.Application.Interfaces;

namespace Crm.Api.Services;

/// <summary>
/// Reads the application version from the <see cref="AssemblyInformationalVersionAttribute"/>
/// of the API assembly, falling back to <c>dev</c> when unavailable.
/// </summary>
public sealed class AssemblyAppVersionProvider : IAppVersionProvider
{
    internal const string DefaultVersion = "dev";

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyAppVersionProvider"/> class
    /// using the <c>Crm.Api</c> assembly.
    /// </summary>
    public AssemblyAppVersionProvider()
        : this(typeof(AssemblyAppVersionProvider).Assembly)
    {
    }

    internal AssemblyAppVersionProvider(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Version = ResolveVersion(assembly);
    }

    /// <inheritdoc />
    public string Version { get; }

    private static string ResolveVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? DefaultVersion
            : informationalVersion.Trim();
    }
}
