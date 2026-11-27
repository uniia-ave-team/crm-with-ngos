using Crm.Domain.Entities;

namespace Crm.Infrastructure.Persistence.Seeders.SystemPresets;

/// <summary>
/// Defines a contract for individual system table template preset definitions.
/// </summary>
public interface ISystemTableTemplate
{
    /// <summary>
    /// Gets optional previous template names used to match and rename existing database rows
    /// in place when a system preset's <see cref="TableTemplate.Name"/> is changed.
    /// </summary>
    IReadOnlyList<string> LegacyNames => [];

    /// <summary>
    /// Constructs and returns the system table template entity with all default fields.
    /// </summary>
    /// <returns>A populated <see cref="TableTemplate"/> instance.</returns>
    TableTemplate GetTemplate();
}
