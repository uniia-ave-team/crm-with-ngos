namespace Crm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Defines a contract for database initialization and seeding operations.
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Executes the seeding logic asynchronously.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task SeedAsync(CancellationToken ct = default);
}
