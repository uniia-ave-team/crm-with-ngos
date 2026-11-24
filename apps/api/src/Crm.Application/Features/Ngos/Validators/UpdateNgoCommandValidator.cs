using Crm.Application.Common.Consts;
using Crm.Application.Features.Ngos.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Ngos.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateNgoCommand"/>.
/// </summary>
public sealed class UpdateNgoCommandValidator : AbstractValidator<UpdateNgoCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNgoCommandValidator"/> class.
    /// </summary>
    public UpdateNgoCommandValidator()
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
