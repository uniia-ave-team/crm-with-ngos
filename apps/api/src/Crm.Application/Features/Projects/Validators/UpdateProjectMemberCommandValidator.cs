using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateProjectMemberCommand"/>.
/// </summary>
public class UpdateProjectMemberCommandValidator : AbstractValidator<UpdateProjectMemberCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProjectMemberCommandValidator"/> class.
    /// </summary>
    public UpdateProjectMemberCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(ValidationMessages.UserIdRequired);

        RuleFor(x => x.RoleTitle)
            .ValidProjectRoleTitle(x => x.RoleTitle);

        RuleFor(x => x.Claim)
            .IsInEnum().WithMessage(ValidationMessages.InvalidProjectClaim);
    }
}
