using Crm.Application.Features.TableTemplates.Commands;
using FluentValidation;

namespace Crm.Application.Features.TableTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="DeleteTableTemplateCommand"/>.
/// </summary>
public sealed class DeleteTableTemplateCommandValidator : AbstractValidator<DeleteTableTemplateCommand>
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
