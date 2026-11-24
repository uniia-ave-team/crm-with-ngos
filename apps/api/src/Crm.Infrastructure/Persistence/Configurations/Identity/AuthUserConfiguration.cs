using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="AuthUser"/> identity entity.
/// </summary>
public sealed class AuthUserConfiguration : IEntityTypeConfiguration<AuthUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthUser> builder)
    {
        builder.ToTable(TableNames.AuthUsers);

        builder.ConfigureGuidV7Key();

        builder.Property(au => au.Email)
            .HasMaxLength(AuthUserValidationConstants.MaxEmailLength);

        builder.Property(au => au.UserName)
            .HasMaxLength(AuthUserValidationConstants.MaxUserNameLength);

        builder.HasOne(au => au.UserProfile)
            .WithOne()
            .HasForeignKey<User>(u => u.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
