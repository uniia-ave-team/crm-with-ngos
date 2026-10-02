using Crm.Domain.Consts.Entities;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

public class AuthRoleConfiguration : IEntityTypeConfiguration<AuthRole>
{
    public void Configure(EntityTypeBuilder<AuthRole> builder)
    {
        builder.ToTable("AuthRoles");

        builder.HasKey(r => r.Id);

        builder.Property(n => n.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(r => r.Name)
            .HasMaxLength(AuthRoleValidationConstants.MaxNameLength);

        builder.Property(r => r.NormalizedName)
            .HasMaxLength(AuthRoleValidationConstants.MaxNameLength);

        builder.Property(r => r.FeminitiveName)
            .HasMaxLength(AuthRoleValidationConstants.MaxFeminitiveNameLength);

        builder.Property(r => r.PluralName)
            .HasMaxLength(AuthRoleValidationConstants.MaxPluralNameLength);
    }
}
