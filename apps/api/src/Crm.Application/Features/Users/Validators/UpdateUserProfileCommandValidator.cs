using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateUserProfileCommand"/>.
/// </summary>
public class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserProfileCommandValidator"/> class.
    /// </summary>
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(UserValidationConstants.MaxFirstNameLength);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(UserValidationConstants.MaxLastNameLength);

        RuleFor(x => x.Patronymic)
            .MaximumLength(UserValidationConstants.MaxPatronymicLength)
            .When(x => !string.IsNullOrEmpty(x.Patronymic));

        RuleFor(x => x.InternalPosition)
            .MaximumLength(UserValidationConstants.MaxInternalPositionLength)
            .When(x => !string.IsNullOrEmpty(x.InternalPosition));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(UserValidationConstants.MaxPhoneNumberLength)
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.Country)
            .MaximumLength(UserValidationConstants.MaxCountryLength)
            .When(x => !string.IsNullOrEmpty(x.Country));

        RuleFor(x => x.EmergencyContact)
            .MaximumLength(UserValidationConstants.MaxEmergencyContactLength)
            .When(x => !string.IsNullOrEmpty(x.EmergencyContact));

        RuleFor(x => x.PreferredLanguage)
            .MaximumLength(UserValidationConstants.MaxPreferredLanguageLength)
            .When(x => !string.IsNullOrEmpty(x.PreferredLanguage));
    }
}
