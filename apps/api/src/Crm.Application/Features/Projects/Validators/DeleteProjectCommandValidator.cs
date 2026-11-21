using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="DeleteProjectCommand"/>.
/// </summary>
public class DeleteProjectCommandValidator : AbstractValidator<DeleteProjectCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteProjectCommandValidator"/> class.
    /// </summary>
    public DeleteProjectCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);
    }
}
