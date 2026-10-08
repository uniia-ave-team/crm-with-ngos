using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
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

        builder.Property(u => u.InternalPosition)
            .HasMaxLength(UserValidationConstants.MaxInternalPositionLength);

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

        builder.HasOne(u => u.Ngo)
               .WithMany(n => n.Users)
               .HasForeignKey(u => u.NgoId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
