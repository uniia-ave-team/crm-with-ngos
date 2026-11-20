using System.Collections.Concurrent;
using Crm.Api.DemoMode.Sandbox;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// An in-memory stand-in for the PostgreSQL sandbox provisioner.
/// </summary>
internal sealed class FakeSandboxDatabase : IDemoSandboxDatabase
{
    public ConcurrentDictionary<string, bool> Databases { get; } = new();

    public int TemplatePreparations { get; private set; }

    public bool FailDrops { get; set; }

    public string GetSandboxDatabaseName(string sandboxId) => "crm_demo_" + sandboxId;

    public string GetConnectionString(string databaseName) => "Host=localhost;Database=" + databaseName;

    public Task PrepareTemplateAsync(CancellationToken cancellationToken)
    {
        TemplatePreparations++;
        Databases.Clear();
        return Task.CompletedTask;
    }

    public Task CreateSandboxAsync(string databaseName, CancellationToken cancellationToken)
    {
        Databases[databaseName] = true;
        return Task.CompletedTask;
    }

    public Task DropSandboxAsync(string databaseName, CancellationToken cancellationToken)
    {
        if (FailDrops)
        {
            throw new InvalidOperationException("drop failed");
        }

        Databases.TryRemove(databaseName, out _);
        return Task.CompletedTask;
    }
}
