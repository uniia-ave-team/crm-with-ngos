using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="TaskTag"/> join entity.
/// </summary>
public class TaskTagConfiguration : IEntityTypeConfiguration<TaskTag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskTag> builder)
    {
        builder.ToTable(TableNames.TaskTag);

        builder.HasKey(tt => new { tt.TagId, tt.TaskId });

        builder.HasIndex(tt => tt.TaskId);
    }
}
