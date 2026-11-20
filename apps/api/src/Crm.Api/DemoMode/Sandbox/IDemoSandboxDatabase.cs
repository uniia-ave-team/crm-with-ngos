namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Creates and drops the sandbox databases. Every sandbox is a copy of a template database
/// that is built once at startup from the migrated and seeded application database.
/// </summary>
public interface IDemoSandboxDatabase
{
    /// <summary>
    /// Builds the database name of a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <returns>The database name.</returns>
    string GetSandboxDatabaseName(string sandboxId);

    /// <summary>
    /// Builds the connection string of a sandbox database.
    /// </summary>
    /// <param name="databaseName">The sandbox database name.</param>
    /// <returns>The connection string.</returns>
    string GetConnectionString(string databaseName);

    /// <summary>
    /// Drops sandbox databases left over from a previous run and rebuilds the template
    /// from the application database.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the template is ready.</returns>
    Task PrepareTemplateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a sandbox database as a copy of the template.
    /// </summary>
    /// <param name="databaseName">The sandbox database name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the database exists.</returns>
    Task CreateSandboxAsync(string databaseName, CancellationToken cancellationToken);

    /// <summary>
    /// Drops a sandbox database together with its open connections.
    /// </summary>
    /// <param name="databaseName">The sandbox database name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the database is gone.</returns>
    Task DropSandboxAsync(string databaseName, CancellationToken cancellationToken);
}
