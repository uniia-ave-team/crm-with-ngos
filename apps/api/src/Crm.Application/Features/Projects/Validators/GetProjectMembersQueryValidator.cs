using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Queries;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetProjectMembersQuery"/>.
/// </summary>
public sealed class GetProjectMembersQueryValidator : AbstractValidator<GetProjectMembersQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectMembersQueryValidator"/> class.
    /// </summary>
    public GetProjectMembersQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);
    }
}
