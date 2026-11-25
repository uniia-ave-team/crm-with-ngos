using Crm.Application.Common.Validators;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateCustomTableFromTemplateCommand"/>.
/// </summary>
public sealed class CreateCustomTableFromTemplateCommandValidator : AbstractValidator<CreateCustomTableFromTemplateCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCustomTableFromTemplateCommandValidator"/> class.
    /// </summary>
    public CreateCustomTableFromTemplateCommandValidator()
    {
        RuleFor(x => x.SourceTemplateId)
            .NotEmpty().WithMessage("Source template ID is required.");

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
    }
}
