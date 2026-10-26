using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateCustomTableFromTemplateCommand"/>.
/// </summary>
public class CreateCustomTableFromTemplateCommandValidator : AbstractValidator<CreateCustomTableFromTemplateCommand>
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
            .WithMessage($"Table name must not exceed {CustomTableValidationConstants.MaxNameLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(CustomTableValidationConstants.MaxDescriptionLength)
            .WithMessage($"Description must not exceed {CustomTableValidationConstants.MaxDescriptionLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
