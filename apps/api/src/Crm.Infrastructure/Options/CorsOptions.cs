namespace Crm.Infrastructure.Options;

/// <summary>
/// Represents configuration options for Cross-Origin Resource Sharing (CORS),
/// defining binding positions and allowed origin endpoints for secure client communication.
/// </summary>
public class CorsOptions
{
    /// <summary>
    /// Gets the configuration section key name used to bind CORS settings from appsettings.
    /// </summary>
    public const string Position = "CorsSettings";

    /// <summary>
    /// Gets the unique policy name identifier used when registering and applying the CORS policy in the HTTP pipeline.
    /// </summary>
    public const string PolicyName = "AllowFrontendClient";

    /// <summary>
    /// Gets or sets the array of allowed domain origins permitted to access the API in production environments.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
