using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="AuthUserRole"/> join entity.
/// </summary>
public sealed class AuthUserRoleConfiguration : IEntityTypeConfiguration<AuthUserRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthUserRole> builder)
    {
        builder.ToTable(TableNames.AuthUserRoles);

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
