using Crm.Application.Common.Validators;
using Crm.Application.Dtos.TableTemplates;
using Crm.Application.Features.TableTemplates.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.TableTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateTableTemplateCommand"/>.
/// </summary>
public sealed class UpdateTableTemplateCommandValidator : AbstractValidator<UpdateTableTemplateCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTableTemplateCommandValidator"/> class.
    /// </summary>
    public UpdateTableTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Template ID is required.");

        RuleFor(x => x.Name)
            .ApplyRequiredTextRules(
                TableTemplateValidationConstants.MaxNameLength,
                "Template name is required.",
                $"Template name must not exceed {TableTemplateValidationConstants.MaxNameLength} characters.");

        RuleFor(x => x.Description)
            .ApplyOptionalTextRules(
                TableTemplateValidationConstants.MaxDescriptionLength,
                $"Description must not exceed {TableTemplateValidationConstants.MaxDescriptionLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field is required.")
            .SetValidator(new IdentifiableFieldListValidator<UpdateTemplateFieldDto>());
    }
}
