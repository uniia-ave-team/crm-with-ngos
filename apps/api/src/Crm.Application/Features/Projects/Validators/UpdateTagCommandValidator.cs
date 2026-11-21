using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateTagCommand"/>.
/// </summary>
public class UpdateTagCommandValidator : AbstractValidator<UpdateTagCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTagCommandValidator"/> class.
    /// </summary>
    public UpdateTagCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage(ValidationMessages.TagIdRequired);

        RuleFor(x => x.Title)
            .ValidTagTitle();
    }
}
