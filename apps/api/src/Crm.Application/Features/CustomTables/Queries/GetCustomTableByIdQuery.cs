using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.CustomTables;
using Mediator;

namespace Crm.Application.Features.CustomTables.Queries;

/// <summary>
/// Represents a query to retrieve full custom table schema including all defined fields.
/// </summary>
/// <param name="Id">The unique identifier of the custom table.</param>
/// <returns>The schema DTO containing table properties and field definitions.</returns>
public record GetCustomTableByIdQuery(
    Guid Id) : IQuery<CustomTableDto>, IHasEntityId;
