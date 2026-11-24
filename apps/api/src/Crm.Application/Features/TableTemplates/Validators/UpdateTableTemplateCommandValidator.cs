using Crm.Application.Common.Consts;
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
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(TableTemplateValidationConstants.MaxNameLength)
            .WithMessage($"Template name must not exceed {TableTemplateValidationConstants.MaxNameLength} characters.")
            .Must(name => name is null || !name.Contains('\0', StringComparison.Ordinal))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed);

        RuleFor(x => x.Description)
            .MaximumLength(TableTemplateValidationConstants.MaxDescriptionLength)
            .WithMessage($"Description must not exceed {TableTemplateValidationConstants.MaxDescriptionLength} characters.")
            .Must(description => description is null || !description.Contains('\0', StringComparison.Ordinal))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field is required.")
            .SetValidator(new IdentifiableFieldListValidator<UpdateTemplateFieldDto>());
    }
}
