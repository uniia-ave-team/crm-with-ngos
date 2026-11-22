using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the singleton <see cref="MaintenanceNotice"/> entity.
/// </summary>
public class MaintenanceNoticeConfiguration : IEntityTypeConfiguration<MaintenanceNotice>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MaintenanceNotice> builder)
    {
        builder.ToTable(
            nameof(MaintenanceNotice),
            table =>
            {
                table.HasCheckConstraint(
                    "CK_MaintenanceNotice_Singleton",
                    $"\"Id\" = {MaintenanceNotice.SingletonId}");
                table.HasCheckConstraint(
                    "CK_MaintenanceNotice_EndsAfterStarts",
                    "\"EndsAt\" > \"StartsAt\"");
            });

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedNever();

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(MaintenanceNoticeValidationConstants.MaxMessageLength);

        builder.Property(n => n.StartsAt)
            .IsRequired();

        builder.Property(n => n.EndsAt)
            .IsRequired();

        builder.Property(n => n.UpdatedAt)
            .IsRequired();
    }
}
