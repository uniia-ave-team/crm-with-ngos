using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public class LoginPageImageConfiguration : IEntityTypeConfiguration<LoginPageImage>
{
    public void Configure(EntityTypeBuilder<LoginPageImage> builder)
    {
        builder.ToTable("LoginPageImages");

        builder.HasKey(lpi => lpi.Id);

        builder.Property(lpi => lpi.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

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
