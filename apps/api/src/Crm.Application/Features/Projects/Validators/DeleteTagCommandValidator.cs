using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="DeleteTagCommand"/>.
/// </summary>
public sealed class DeleteTagCommandValidator : AbstractValidator<DeleteTagCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTagCommandValidator"/> class.
    /// </summary>
    public DeleteTagCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage(ValidationMessages.TagIdRequired);
    }
}
