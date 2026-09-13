using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

public class AuthUserConfiguration : IEntityTypeConfiguration<AuthUser>
{
    public void Configure(EntityTypeBuilder<AuthUser> builder)
    {
        builder.ToTable("AuthUsers");

        builder.HasKey(au => au.Id);

        builder.Property(n => n.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(au => au.Email)
            .HasMaxLength(AuthUserValidationConstants.MaxEmailLength);

        builder.Property(au => au.UserName)
            .HasMaxLength(AuthUserValidationConstants.MaxUserNameLength);
    }
}
