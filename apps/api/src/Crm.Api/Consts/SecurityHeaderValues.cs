namespace Crm.Api.Consts;

/// <summary>
/// Provides standard HTTP security header value constants.
/// </summary>
public static class SecurityHeaderValues
{
    /// <summary>
    /// Prevents MIME-type sniffing by browsers for <c>X-Content-Type-Options</c>.
    /// </summary>
    public const string NoSniff = "nosniff";
}
