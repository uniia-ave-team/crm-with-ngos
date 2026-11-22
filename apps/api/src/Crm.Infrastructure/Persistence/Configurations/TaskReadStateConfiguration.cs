using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="TaskReadState"/> entity.
/// </summary>
public sealed class TaskReadStateConfiguration : IEntityTypeConfiguration<TaskReadState>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskReadState> builder)
    {
        builder.ToTable(TableNames.TaskReadState);

        builder.HasKey(rs => new { rs.CommentId, rs.UserId });

        builder.HasOne(rs => rs.Comment)
            .WithMany(c => c.ReadStates)
            .HasForeignKey(rs => rs.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rs => rs.User)
            .WithMany()
            .HasForeignKey(rs => rs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rs => rs.UserId);

        builder.HasQueryFilter(rs => !rs.Comment.Task.IsDeleted && !rs.Comment.Task.Project.IsDeleted);
    }
}
