using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTemplates;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Domain.Consts.Entities.ValidationConstants;
using FluentValidation;

namespace Crm.Application.Features.CustomTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateTableTemplateCommand"/>.
/// </summary>
public class UpdateTableTemplateCommandValidator : AbstractValidator<UpdateTableTemplateCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTableTemplateCommandValidator"/> class.
    /// </summary>
    public UpdateTableTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Template ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(TableTemplateValidationConstants.MaxNameLength).WithMessage($"Template name must not exceed {TableTemplateValidationConstants.MaxNameLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(TableTemplateValidationConstants.MaxDescriptionLength).WithMessage($"Description must not exceed {TableTemplateValidationConstants.MaxDescriptionLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field is required.")
            .SetValidator(new IdentifiableFieldListValidator<UpdateTemplateFieldDto>());
    }
}
