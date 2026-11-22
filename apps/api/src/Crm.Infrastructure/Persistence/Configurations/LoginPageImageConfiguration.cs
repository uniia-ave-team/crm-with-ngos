using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="LoginPageImage"/> entity.
/// </summary>
public sealed class LoginPageImageConfiguration : IEntityTypeConfiguration<LoginPageImage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LoginPageImage> builder)
    {
        builder.ToTable(TableNames.LoginPageImages);

        builder.ConfigureGuidV7Key();

        builder.Property(lpi => lpi.Url)
            .IsRequired()
            .HasMaxLength(LoginPageImageValidationConstants.MaxImageUrlLength);

        builder.HasOne(lpi => lpi.Ngo)
            .WithMany()
            .HasForeignKey(lpi => lpi.NgoId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
