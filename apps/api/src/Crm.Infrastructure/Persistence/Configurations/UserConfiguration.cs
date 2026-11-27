using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="User"/> profile entity.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(UserValidationConstants.MaxFirstNameLength);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(UserValidationConstants.MaxLastNameLength);

        builder.Property(u => u.Patronymic)
            .HasMaxLength(UserValidationConstants.MaxPatronymicLength);

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(UserValidationConstants.MaxPhoneNumberLength);

        builder.Property(u => u.Country)
            .HasMaxLength(UserValidationConstants.MaxCountryLength);

        builder.Property(u => u.EmergencyContact)
            .HasMaxLength(UserValidationConstants.MaxEmergencyContactLength);

        builder.Property(u => u.PreferredLanguage)
            .HasMaxLength(UserValidationConstants.MaxPreferredLanguageLength);

        builder.Property(u => u.Pronouns)
            .HasMaxLength(UserValidationConstants.MaxPronounsLength);

        builder.Property(u => u.Avatar)
            .HasMaxLength(UserValidationConstants.MaxAvatarUrlLength);

        builder.Property(u => u.CustomFields)
            .ConfigureJsonbColumn()
            .HasDefaultValueSql("'{}'::jsonb");

        builder.HasOne(u => u.Ngo)
            .WithMany(n => n.Users)
            .HasForeignKey(u => u.NgoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
