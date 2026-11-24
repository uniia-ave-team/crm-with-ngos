using Crm.Domain.Constants.Validation;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="AuthRole"/> identity entity.
/// </summary>
public sealed class AuthRoleConfiguration : IEntityTypeConfiguration<AuthRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthRole> builder)
    {
        builder.ToTable(TableNames.AuthRoles);

        builder.ConfigureGuidV7Key();

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
