namespace Crm.Domain.Consts.Entities.GeneralConstants;

/// <summary>
/// Provides constants for <see cref="Domain.Entities.CustomRecord"/> validation, schema properties, and search parameters.
/// </summary>
public static class CustomRecordConstants
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
