using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="CreateTagCommand"/>.
/// </summary>
public sealed class CreateTagCommandValidator : AbstractValidator<CreateTagCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTagCommandValidator"/> class.
    /// </summary>
    public CreateTagCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.Title)
            .ValidTagTitle();
    }
}
