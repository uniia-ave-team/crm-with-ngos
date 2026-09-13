using Crm.Application.Dtos.Ngo;
using Crm.Application.Dtos.Ngo.Queries;
using Crm.Application.Features.Ngos.Mappings;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;

namespace Crm.Application.Features.Ngos.Queries;

/// <summary>
/// Handles the <see cref="GetNgoQuery"/> to fetch and map the NGO details.
/// </summary>
/// <param name="repository">The repository used for accessing NGO data.</param>
public class GetNgoQueryHandler(INgoRepository repository) : IRequestHandler<GetNgoQuery, NgoDto?>
{
    /// <summary>
    /// Handles the retrieval process for the NGO asynchronously.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the mapped <see cref="NgoDto"/>.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    public async Task<NgoDto?> Handle(GetNgoQuery request, CancellationToken cancellationToken)
    {
        var ngo = await repository.GetAsync(cancellationToken);

        return ngo.ToDto();
    }
}
