using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the singleton <see cref="MaintenanceNotice"/> entity.
/// </summary>
public sealed class MaintenanceNoticeConfiguration : IEntityTypeConfiguration<MaintenanceNotice>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MaintenanceNotice> builder)
    {
        builder.ToTable(
            TableNames.MaintenanceNotice,
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

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.UpdatedAt)
            .IsRequired();

        builder.ConfigureAuthorTracking(n => n.Creator, n => n.Updater);
    }
}
