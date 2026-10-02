using Crm.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

public class AuthUserRoleConfiguration : IEntityTypeConfiguration<AuthUserRole>
{
    public void Configure(EntityTypeBuilder<AuthUserRole> builder)
    {
        builder.ToTable("AuthUserRoles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .IsRequired();

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .IsRequired();
    }
}
