using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateProjectCommand"/>.
/// </summary>
public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProjectCommandValidator"/> class.
    /// </summary>
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.Title)
            .ValidProjectTitle();

        RuleFor(x => x.Description)
            .ValidProjectDescription();

        RuleFor(x => x.Visibility)
            .IsInEnum().WithMessage(ValidationMessages.InvalidProjectVisibility);

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage(ValidationMessages.StartDateRequired);

        RuleFor(x => x.EndDate)
            .Must((command, endDate) => endDate!.Value.ToUtc() >= command.StartDate.ToUtc())
            .WithMessage(ValidationMessages.EndDateBeforeStartDate)
            .When(x => x.EndDate.HasValue);
    }
}
