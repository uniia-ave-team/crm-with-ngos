using Crm.Application.Dtos.TableTemplates;
using Mediator;

namespace Crm.Application.Features.TableTemplates.Queries;

/// <summary>
/// Represents a query to retrieve a specific table template by its identifier.
/// </summary>
/// <param name="Id">The unique identifier of the table template.</param>
/// <returns>The detailed DTO containing template properties and field blueprints.</returns>
public record GetTableTemplateByIdQuery(
    Guid Id) : IQuery<TableTemplateDto>;
