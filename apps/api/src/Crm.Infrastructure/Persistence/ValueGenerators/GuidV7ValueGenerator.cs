using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Crm.Infrastructure.Persistence.ValueGenerators;

/// <summary>
/// Represents a value generator that automatically creates time-ordered Version 7 GUIDs
/// for Entity Framework Core properties.
/// </summary>
public class GuidV7ValueGenerator : ValueGenerator<Guid>
{
    /// <inheritdoc />
    public override bool GeneratesTemporaryValues => false;

    /// <inheritdoc />
    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
}
