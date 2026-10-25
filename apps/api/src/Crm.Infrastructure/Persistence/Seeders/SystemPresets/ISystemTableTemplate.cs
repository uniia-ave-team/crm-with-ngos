using Crm.Domain.Entities;

namespace Crm.Infrastructure.Persistence.Seeders.SystemPresets;

/// <summary>
/// Defines a contract for individual system table template preset definitions.
/// </summary>
public interface ISystemTableTemplate
{
    /// <summary>
    /// Constructs and returns the system table template entity with all default fields.
    /// </summary>
    /// <returns>A populated <see cref="TableTemplate"/> instance.</returns>
    TableTemplate GetTemplate();
}
