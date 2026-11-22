namespace Crm.Infrastructure.Persistence.Consts;

/// <summary>
/// Provides PostgreSQL full-text search and EF Core shadow property constants for <see cref="Domain.Entities.CustomRecord"/>.
/// </summary>
public static class CustomRecordSearchConstants
{
    /// <summary>
    /// The name of the EF Core shadow property used for full-text search indexing.
    /// </summary>
    public const string SearchVectorPropertyName = "SearchVector";

    /// <summary>
    /// The PostgreSQL text search configuration dictionary (e.g., 'simple', 'english').
    /// </summary>
    public const string SearchVectorConfiguration = "simple";
}
