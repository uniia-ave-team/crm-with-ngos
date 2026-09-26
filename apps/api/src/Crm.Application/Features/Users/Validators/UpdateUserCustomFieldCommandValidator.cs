using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateUserCustomFieldCommand"/>.
/// </summary>
public class UpdateUserCustomFieldCommandValidator : AbstractValidator<UpdateUserCustomFieldCommand>
{
    public UpdateUserCustomFieldCommandValidator()
    {
        RuleFor(x => x.CustomFieldId)
            .NotEmpty().WithMessage("Custom field ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Custom field value is required.")
            .MaximumLength(UserCustomFieldValidationConstants.MaxValueLength)
            .WithMessage($"Custom field value must not exceed {UserCustomFieldValidationConstants.MaxValueLength} characters.");
    }
}
