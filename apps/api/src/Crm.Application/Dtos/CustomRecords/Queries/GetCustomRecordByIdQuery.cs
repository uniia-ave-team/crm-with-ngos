using MediatR;

namespace Crm.Application.Dtos.CustomRecords.Queries;

/// <summary>
/// Represents a query to retrieve a single custom record by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the custom record.</param>
/// <returns>The detailed DTO representation of the custom record.</returns>
public record GetCustomRecordByIdQuery(
    Guid Id) : IRequest<CustomRecordDto>;
