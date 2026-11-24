using Crm.Application.Common.Consts;
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
            .NotEmpty().WithMessage("Table name is required.")
            .MaximumLength(CustomTableValidationConstants.MaxNameLength)
            .WithMessage($"Table name must not exceed {CustomTableValidationConstants.MaxNameLength} characters.")
            .Must(name => name is null || !name.Contains('\0', StringComparison.Ordinal))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed);

        RuleFor(x => x.Description)
            .MaximumLength(CustomTableValidationConstants.MaxDescriptionLength)
            .WithMessage($"Description must not exceed {CustomTableValidationConstants.MaxDescriptionLength} characters.")
            .Must(description => description is null || !description.Contains('\0', StringComparison.Ordinal))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
