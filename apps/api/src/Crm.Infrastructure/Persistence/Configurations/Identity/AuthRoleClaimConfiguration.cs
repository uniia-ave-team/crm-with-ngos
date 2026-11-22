using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="AuthRoleClaim"/> entity.
/// </summary>
public sealed class AuthRoleClaimConfiguration : IEntityTypeConfiguration<AuthRoleClaim>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthRoleClaim> builder)
    {
        builder.ToTable(TableNames.AuthRoleClaims);

        builder.HasKey(rc => rc.Id);

        builder.HasOne(rc => rc.Role)
            .WithMany(r => r.RoleClaims)
            .HasForeignKey(rc => rc.RoleId)
            .IsRequired();
    }
}
