using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Crm.Infrastructure.Persistence.ValueGenerators;

/// <summary>
/// Генератор значень для автоматичного створення Guid Version 7 через Entity Framework Core.
/// </summary>
public class GuidV7ValueGenerator : ValueGenerator<Guid>
{
    /// <inheritdoc />
    public override bool GeneratesTemporaryValues => false;

    /// <inheritdoc />
    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
}
