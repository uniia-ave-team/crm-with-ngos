namespace Crm.Infrastructure.Options;

/// <summary>
/// Configuration settings for trusted reverse proxy networks used by forwarded headers middleware.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    /// <summary>
    /// Configuration section name for forwarded headers settings.
    /// </summary>
    public const string Position = "ForwardedHeaders";

    /// <summary>
    /// Default trusted CIDR networks: IPv4/IPv6 loopback and RFC 1918 private networks
    /// so Docker and local reverse proxies work out of the box while public clients cannot spoof headers.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultKnownNetworks =
    [
        "127.0.0.0/8",
        "::1/128",
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
    ];

    /// <summary>
    /// Gets or sets the list of CIDR ranges trusted to send <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> headers.
    /// When omitted from configuration, <see cref="DefaultKnownNetworks"/> is used.
    /// </summary>
    public string[]? KnownNetworks { get; set; }
}
