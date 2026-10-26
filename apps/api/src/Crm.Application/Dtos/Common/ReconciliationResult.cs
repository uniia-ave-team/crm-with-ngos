using Crm.Domain.Interfaces;

namespace Crm.Application.Dtos.Common;

/// <summary>
/// Encapsulates the result of a schema reconciliation operation, categorizing field entities
/// into removed, newly added, and matched/updated collections.
/// </summary>
/// <typeparam name="TEntity">The domain field entity type implementing <see cref="IIdentifiableField"/>.</typeparam>
/// <param name="Removed">The collection of existing entities that were not present in the incoming DTO payload and should be removed.</param>
/// <param name="Added">The collection of newly created entities instantiated from incoming DTOs without an ID.</param>
/// <param name="Matched">The collection of existing entities that matched incoming DTOs by ID and were adapted in-place.</param>
public record ReconciliationResult<TEntity>(
    IReadOnlyList<TEntity> Removed,
    IReadOnlyList<TEntity> Added,
    IReadOnlyList<TEntity> Matched)
    where TEntity : IIdentifiableField;
