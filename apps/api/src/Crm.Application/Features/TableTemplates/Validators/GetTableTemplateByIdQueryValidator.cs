using Crm.Application.Features.TableTemplates.Queries;
using FluentValidation;

namespace Crm.Application.Features.TableTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetTableTemplateByIdQuery"/>.
/// </summary>
public sealed class GetTableTemplateByIdQueryValidator : AbstractValidator<GetTableTemplateByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTableTemplateByIdQueryValidator"/> class.
    /// </summary>
    public GetTableTemplateByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Template ID is required.");
    }
}
