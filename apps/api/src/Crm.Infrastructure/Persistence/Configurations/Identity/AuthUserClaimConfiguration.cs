using Crm.Infrastructure.Persistence.Consts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the Entity Framework Core mapping for <see cref="IdentityUserClaim{TKey}"/>.
/// </summary>
public sealed class AuthUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable(TableNames.AuthUserClaims);

        builder.HasKey(uc => uc.Id);
    }
}
