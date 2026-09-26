using Crm.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

public class AuthRoleClaimConfiguration : IEntityTypeConfiguration<AuthRoleClaim>
{
    public void Configure(EntityTypeBuilder<AuthRoleClaim> builder)
    {
        builder.ToTable("AuthRoleClaims");

        builder.HasKey(rc => rc.Id);

        builder.HasOne(rc => rc.Role)
            .WithMany(r => r.RoleClaims)
            .HasForeignKey(rc => rc.RoleId)
            .IsRequired();
    }
}
