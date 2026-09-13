using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public class NgoConfiguration : IEntityTypeConfiguration<Ngo>
{
    public void Configure(EntityTypeBuilder<Ngo> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(n => n.Name)
            .IsRequired()
            .HasMaxLength(NgoValidationConstants.MaxNameLength);

        builder.Property(n => n.Description)
            .HasMaxLength(NgoValidationConstants.MaxDescriptionLength);

        builder.Property(n => n.LogoUrl)
            .HasMaxLength(NgoValidationConstants.MaxLogoUrlLength);
    }
}
