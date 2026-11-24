using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Queries;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetProjectByIdQuery"/>.
/// </summary>
public sealed class GetProjectByIdQueryValidator : AbstractValidator<GetProjectByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectByIdQueryValidator"/> class.
    /// </summary>
    public GetProjectByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);
    }
}
