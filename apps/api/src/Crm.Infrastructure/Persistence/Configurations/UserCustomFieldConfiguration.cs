using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public class UserCustomFieldConfiguration : IEntityTypeConfiguration<UserCustomField>
{
    public void Configure(EntityTypeBuilder<UserCustomField> builder)
    {
        builder.HasKey(cf => cf.Id);

        builder.HasIndex(cf => new { cf.UserId, cf.Key })
            .IsUnique();

        builder.Property(n => n.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(cf => cf.Key)
            .IsRequired()
            .HasMaxLength(UserCustomFieldValidationConstants.MaxKeyLength);

        builder.Property(cf => cf.Value)
            .IsRequired()
            .HasMaxLength(UserCustomFieldValidationConstants.MaxValueLength);

        builder.HasOne(cf => cf.User)
               .WithMany(u => u.CustomFields)
               .HasForeignKey(cf => cf.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
