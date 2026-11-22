using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="RemoveProjectMemberCommand"/>.
/// </summary>
public sealed class RemoveProjectMemberCommandValidator : AbstractValidator<RemoveProjectMemberCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveProjectMemberCommandValidator"/> class.
    /// </summary>
    public RemoveProjectMemberCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(ValidationMessages.UserIdRequired);
    }
}
