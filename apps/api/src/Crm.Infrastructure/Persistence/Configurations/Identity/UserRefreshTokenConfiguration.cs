using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the database mapping for the <see cref="UserRefreshToken"/> entity.
/// </summary>
public sealed class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable(TableNames.UserRefreshTokens);

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(UserRefreshTokenValidationConstants.MaxTokenLength);

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.HasIndex(rt => rt.Token)
            .IsUnique();

        builder.HasIndex(rt => rt.UserId);
        builder.HasIndex(rt => rt.ExpiresAt);

        builder.HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
