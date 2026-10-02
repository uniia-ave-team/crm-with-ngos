namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines a generic repository interface for performing standard CRUD operations
/// on entities of a specific type.
/// </summary>
/// <typeparam name="T">The type of the entity. Must implement <see cref="IEntity"/>.</typeparam>
public interface IGenericRepository<T> :
    IQueryRepository<T>,
    IProjectingRepository,
    IPagedProjectingRepository<T>,
    ICommandRepository<T>,
    IExistenceChecker
    where T : IEntity
{
}
