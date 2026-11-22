using Crm.Api.Options;
using Microsoft.AspNetCore.HttpOverrides;
using SystemIpNetwork = System.Net.IPNetwork;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring trusted proxy networks for forwarded headers processing.
/// </summary>
public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Configures <see cref="ForwardedHeadersOptions"/> so that <c>X-Forwarded-For</c> and
    /// <c>X-Forwarded-Proto</c> are accepted only from the CIDR ranges defined in
    /// <c>ForwardedHeaders:KnownNetworks</c> (or loopback and private networks by default).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
    /// <param name="configuration">The application configuration containing optional <c>ForwardedHeaders:KnownNetworks</c> entries.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when any configured entry is not a valid CIDR network.</exception>
    public static IServiceCollection AddCustomForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var networks = ResolveKnownNetworks(configuration);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            if (networks.Count == 0)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }

    internal static IReadOnlyList<SystemIpNetwork> ResolveKnownNetworks(IConfiguration configuration)
    {
        var knownNetworksSection = configuration.GetSection(
            $"{ForwardedHeadersSettings.Position}:{nameof(ForwardedHeadersSettings.KnownNetworks)}");

        IReadOnlyList<string> rawCidrs = knownNetworksSection.Exists()
            ? knownNetworksSection.Get<string[]>() ?? []
            : ForwardedHeadersSettings.DefaultKnownNetworks;

        var parsedNetworks = new List<SystemIpNetwork>(rawCidrs.Count);

        foreach (var rawCidr in rawCidrs)
        {
            if (string.IsNullOrWhiteSpace(rawCidr) || !SystemIpNetwork.TryParse(rawCidr.Trim(), out var network))
            {
                throw new InvalidOperationException(
                    $"Invalid CIDR network '{rawCidr}' in configuration section '{ForwardedHeadersSettings.Position}:{nameof(ForwardedHeadersSettings.KnownNetworks)}'.");
            }

            parsedNetworks.Add(network);
        }

        return parsedNetworks;
    }
}
