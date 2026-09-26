using Crm.Domain.Consts.Entities;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the database mapping for the <see cref="UserRefreshToken"/> entity.
/// </summary>
public class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable("UserRefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(u => u.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(UserRefreshTokenValidationConstants.MaxTokenLength);

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.HasIndex(x => x.Token)
            .IsUnique();

        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
