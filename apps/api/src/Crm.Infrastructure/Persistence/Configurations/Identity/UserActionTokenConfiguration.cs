using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the database mapping for the <see cref="UserActionToken"/> entity.
/// </summary>
public sealed class UserActionTokenConfiguration : IEntityTypeConfiguration<UserActionToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserActionToken> builder)
    {
        builder.ToTable(TableNames.UserActionTokens);

        builder.ConfigureGuidV7Key();

        builder.Property(t => t.Type)
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(UserActionTokenValidationConstants.TokenHashLength);

        builder.Property(t => t.TargetEmail)
            .IsRequired()
            .HasMaxLength(UserActionTokenValidationConstants.MaxTargetEmailLength);

        builder.Property(t => t.NormalizedTargetEmail)
            .IsRequired()
            .HasMaxLength(UserActionTokenValidationConstants.MaxTargetEmailLength);

        builder.Property(t => t.EncryptedMetadata)
            .IsRequired()
            .HasMaxLength(UserActionTokenValidationConstants.MaxEncryptedMetadataLength);

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasIndex(t => t.TokenHash)
            .IsUnique();

        builder.HasIndex(t => new { t.Type, t.ExpiresAt });
        builder.HasIndex(t => new { t.Type, t.NormalizedTargetEmail });
        builder.HasIndex(t => t.TargetUserId);
        builder.HasIndex(t => t.ExpiresAt);

        builder.HasOne(t => t.TargetUser)
            .WithMany()
            .HasForeignKey(t => t.TargetUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
