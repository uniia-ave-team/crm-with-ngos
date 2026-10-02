using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="AddUserCustomFieldCommand"/>.
/// </summary>
public class AddUserCustomFieldCommandValidator : AbstractValidator<AddUserCustomFieldCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddUserCustomFieldCommandValidator"/> class.
    /// </summary>
    public AddUserCustomFieldCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Custom field key is required.")
            .Matches("^[a-zA-Z0-9_а-яА-ЯіІїЇєЄґҐ-]+$")
            .WithMessage("Custom field key can only contain letters, numbers, hyphens, and underscores.")
            .MaximumLength(UserCustomFieldValidationConstants.MaxKeyLength)
            .WithMessage($"Custom field key must not exceed {UserCustomFieldValidationConstants.MaxKeyLength} characters.");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Custom field value is required.")
            .MaximumLength(UserCustomFieldValidationConstants.MaxValueLength)
            .WithMessage($"Custom field value must not exceed {UserCustomFieldValidationConstants.MaxValueLength} characters.");
    }
}
