using Crm.Application.Dtos.Ngo.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Ngos.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateNgoCommand"/>.
/// </summary>
public class CreateNgoCommandValidator : AbstractValidator<CreateNgoCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateNgoCommandValidator"/> class.
    /// </summary>
    public CreateNgoCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("NGO name is required.")
            .MaximumLength(NgoValidationConstants.MaxNameLength)
                .WithMessage($"NGO name must not exceed {NgoValidationConstants.MaxNameLength} characters.");
    }
}
