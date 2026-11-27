using Crm.Application.Common.Validators;
using Crm.Application.Dtos.Common;
using Crm.Application.Features.TableTemplates.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.TableTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateTableTemplateCommand"/>.
/// </summary>
public sealed class CreateTableTemplateCommandValidator : AbstractValidator<CreateTableTemplateCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTableTemplateCommandValidator"/> class.
    /// </summary>
    public CreateTableTemplateCommandValidator()
    {
        RuleFor(x => x.Name)
            .ApplyRequiredTextRules(
                CustomTableValidationConstants.MaxNameLength,
                "Template name is required.",
                $"Template name must not exceed {CustomTableValidationConstants.MaxNameLength} characters.");

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
