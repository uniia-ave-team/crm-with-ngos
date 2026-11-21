using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="ProjectTask"/> entity.
/// </summary>
public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable(TableNames.ProjectTask);

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(TaskValidationConstants.MaxTitleLength);

        builder.Property(t => t.Description)
            .HasMaxLength(TaskValidationConstants.MaxDescriptionLength);

        builder.Property(t => t.Status)
            .IsRequired();

        builder.Property(t => t.Priority)
            .IsRequired();

        builder.Property(t => t.StartDate)
            .IsRequired();

        builder.Property(t => t.EndDateWasAuto)
            .IsRequired();

        builder.OwnsMany(t => t.ExternalLinks, nav => nav.ToJson());

        builder.Property(t => t.Metadata)
            .HasColumnType(nameof(NpgsqlDbType.Jsonb).ToLowerInvariant())
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired();

        builder.HasOne(t => t.Topic)
            .WithMany(topic => topic.Tasks)
            .HasForeignKey(t => t.TopicId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.AssignedUsers)
            .WithMany()
            .UsingEntity<TaskUser>(
                right => right
                    .HasOne(tu => tu.User)
                    .WithMany()
                    .HasForeignKey(tu => tu.UserId)
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne(tu => tu.Task)
                    .WithMany()
                    .HasForeignKey(tu => tu.TaskId)
                    .OnDelete(DeleteBehavior.Cascade));

        builder.HasMany(t => t.Tags)
            .WithMany(tag => tag.Tasks)
            .UsingEntity<TaskTag>(
                right => right
                    .HasOne(tt => tt.Tag)
                    .WithMany()
                    .HasForeignKey(tt => tt.TagId)
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne(tt => tt.Task)
                    .WithMany()
                    .HasForeignKey(tt => tt.TaskId)
                    .OnDelete(DeleteBehavior.Cascade));

        builder.HasMany(t => t.Comments)
            .WithOne(c => c.Task)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Attachments)
            .WithOne(a => a.Task)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TopicId);
        builder.HasIndex(t => new { t.ProjectId, t.TopicId });
        builder.HasIndex(t => new { t.ProjectId, t.Status });
        builder.HasIndex(t => new { t.ProjectId, t.Priority });
        builder.HasIndex(t => new { t.ProjectId, t.StartDate });
        builder.HasIndex(t => new { t.ProjectId, t.EndDate });
        builder.HasIndex(t => new { t.ProjectId, t.CreatedAt });
    }
}
