using Crm.Application.Common.Validators;
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
            .ApplyRequiredTextRules(
                NgoValidationConstants.MaxNameLength,
                "NGO name is required.",
                $"NGO name must not exceed {NgoValidationConstants.MaxNameLength} characters.");
    }
}
