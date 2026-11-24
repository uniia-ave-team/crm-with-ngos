namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents a domain entity that records the identifiers of the users who created and last updated it.
/// </summary>
public interface IAuthorTrackedEntity
{
    /// <summary>
    /// Gets or sets the optional unique identifier of the user who created the entity.
    /// Set to <c>null</c> for system operations or if the creator user is deleted.
    /// </summary>
    Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who last updated the entity.
    /// Set to <c>null</c> for system operations or if the updater user is deleted.
    /// </summary>
    Guid? UpdatedBy { get; set; }
}
