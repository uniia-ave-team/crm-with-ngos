using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="ProjectMember"/> cross-table entity.
/// </summary>
public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasKey(m => new { m.ProjectId, m.UserId });

        builder.Property(m => m.RoleTitle)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxRoleTitleLength);

        builder.Property(m => m.Claim)
            .IsRequired();

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
