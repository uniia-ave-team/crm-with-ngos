using Crm.Infrastructure.Persistence.Consts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the Entity Framework Core mapping for <see cref="IdentityUserLogin{TKey}"/>.
/// </summary>
public sealed class AuthUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable(TableNames.AuthUserLogins);

        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey });
    }
}
