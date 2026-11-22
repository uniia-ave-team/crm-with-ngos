namespace Crm.Infrastructure.Consts;

/// <summary>
/// Provides health check registration names for infrastructure dependencies.
/// </summary>
public static class HealthCheckNames
{
    /// <summary>
    /// The health check name for the PostgreSQL Entity Framework Core DbContext.
    /// </summary>
    public const string DbContext = "postgresql";
}
