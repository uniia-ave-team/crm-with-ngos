namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents a persisted field domain entity that implements <see cref="IEntity"/> and <see cref="IIdentifiableField"/>.
/// </summary>
public interface IEntityField : IEntity, IIdentifiableField
{
    /// <summary>
    /// Gets the default bridge implementation mapping non-nullable <see cref="IEntity.Id"/> to <see cref="IIdentifiableField.Id"/>.
    /// </summary>
    Guid? IIdentifiableField.Id => ((IEntity)this).Id == Guid.Empty ? null : ((IEntity)this).Id;
}
