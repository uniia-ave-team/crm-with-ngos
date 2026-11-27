using Crm.Application.Common.Validators;
using Crm.Application.Dtos.Common;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateCustomTableCommand"/>.
/// </summary>
public sealed class CreateCustomTableCommandValidator : AbstractValidator<CreateCustomTableCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCustomTableCommandValidator"/> class.
    /// </summary>
    public CreateCustomTableCommandValidator()
    {
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
            .SetValidator(new FieldListValidator<FieldDefinitionDto>());
    }
}
