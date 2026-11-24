using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="Ngo"/> entity.
/// </summary>
public sealed class NgoConfiguration : IEntityTypeConfiguration<Ngo>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Ngo> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.Property(n => n.Name)
            .IsRequired()
            .HasMaxLength(NgoValidationConstants.MaxNameLength);

        builder.Property(n => n.Logo)
            .HasMaxLength(NgoValidationConstants.MaxLogoUrlLength);
    }
}
