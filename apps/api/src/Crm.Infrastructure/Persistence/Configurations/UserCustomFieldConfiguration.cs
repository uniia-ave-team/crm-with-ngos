using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="UserCustomField"/> entity.
/// </summary>
public sealed class UserCustomFieldConfiguration : IEntityTypeConfiguration<UserCustomField>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserCustomField> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.HasIndex(cf => new { cf.UserId, cf.Key })
            .IsUnique();

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
