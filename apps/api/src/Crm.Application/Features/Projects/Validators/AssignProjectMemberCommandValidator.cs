using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="AssignProjectMemberCommand"/>.
/// </summary>
public sealed class AssignProjectMemberCommandValidator : AbstractValidator<AssignProjectMemberCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssignProjectMemberCommandValidator"/> class.
    /// </summary>
    public AssignProjectMemberCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(ValidationMessages.UserIdRequired);

        RuleFor(x => x.RoleTitle)
            .ValidProjectRoleTitle();

        RuleFor(x => x.Claim)
            .IsInEnum().WithMessage(ValidationMessages.InvalidProjectClaim);
    }
}
