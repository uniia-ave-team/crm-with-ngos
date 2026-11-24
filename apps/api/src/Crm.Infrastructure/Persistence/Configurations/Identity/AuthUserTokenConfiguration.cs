using Crm.Infrastructure.Persistence.Consts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Configures the Entity Framework Core mapping for <see cref="IdentityUserToken{TKey}"/>.
/// </summary>
public sealed class AuthUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable(TableNames.AuthUserTokens);

        builder.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
    }
}
