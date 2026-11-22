using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="TaskUser"/> join entity.
/// </summary>
public sealed class TaskUserConfiguration : IEntityTypeConfiguration<TaskUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskUser> builder)
    {
        builder.ToTable(TableNames.TaskUser);

        builder.HasKey(tu => new { tu.TaskId, tu.UserId });

        builder.HasIndex(tu => tu.UserId);

        builder.HasQueryFilter(tu => !tu.Task.IsDeleted && !tu.Task.Project.IsDeleted);
    }
}
