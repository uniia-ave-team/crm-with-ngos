using Crm.Application.Dtos.Ngo.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Ngos.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateNgoCommand"/>.
/// </summary>
public class UpdateNgoCommandValidator : AbstractValidator<UpdateNgoCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNgoCommandValidator"/> class.
    /// </summary>
    public UpdateNgoCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("NGO name is required.")
            .MaximumLength(NgoValidationConstants.MaxNameLength)
                .WithMessage($"NGO name must not exceed {NgoValidationConstants.MaxNameLength} characters.");
    }
}
