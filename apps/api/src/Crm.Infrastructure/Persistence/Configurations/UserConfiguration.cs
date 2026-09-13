using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

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

        builder.HasOne(u => u.AuthUser)
               .WithOne(au => au.UserProfile)
               .HasForeignKey<User>(u => u.Id)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.Ngo)
               .WithMany(n => n.Users)
               .HasForeignKey(u => u.NgoId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
