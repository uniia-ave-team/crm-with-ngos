using Crm.Application.Common.Consts;
using Crm.Application.Features.Ngos.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Ngos.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateNgoCommand"/>.
/// </summary>
public sealed class CreateNgoCommandValidator : AbstractValidator<CreateNgoCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateNgoCommandValidator"/> class.
    /// </summary>
    public CreateNgoCommandValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("NGO name is required.")
            .MaximumLength(NgoValidationConstants.MaxNameLength)
                .WithMessage($"NGO name must not exceed {NgoValidationConstants.MaxNameLength} characters.")
            .Must(name => !name.Contains('\0'))
                .WithMessage(ValidationMessages.NullCharacterNotAllowed);
    }
}
