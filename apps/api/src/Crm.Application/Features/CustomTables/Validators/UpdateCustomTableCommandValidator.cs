using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateCustomTableCommand"/>.
/// </summary>
public sealed class UpdateCustomTableCommandValidator : AbstractValidator<UpdateCustomTableCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCustomTableCommandValidator"/> class.
    /// </summary>
    public UpdateCustomTableCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Table ID is required.");

        RuleFor(x => x.Name)
            .ApplyRequiredTextRules(
                CustomTableValidationConstants.MaxNameLength,
                "Table name is required.",
                $"Table name must not exceed {CustomTableValidationConstants.MaxNameLength} characters.");

        RuleFor(x => x.Description)
            .ApplyOptionalTextRules(
                CustomTableValidationConstants.MaxDescriptionLength,
                $"Description must not exceed {CustomTableValidationConstants.MaxDescriptionLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field is required.")
            .SetValidator(new IdentifiableFieldListValidator<UpdateCustomFieldDto>());
    }
}
