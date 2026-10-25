using Crm.Application.Dtos.CustomTemplates.Commands;
using FluentValidation;

namespace Crm.Application.Features.CustomTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="DeleteTableTemplateCommand"/>.
/// </summary>
public class DeleteTableTemplateCommandValidator : AbstractValidator<DeleteTableTemplateCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTableTemplateCommandValidator"/> class.
    /// </summary>
    public DeleteTableTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Template ID is required.");
    }
}
