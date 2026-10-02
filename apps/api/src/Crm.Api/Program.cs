using System.Reflection;
using Crm.Api.Extensions;
using Crm.Infrastructure.Extensions;
using Crm.Infrastructure.Persistence.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureServices();

var app = builder.Build();

app.ConfigurePipeline();

// Build-time OpenAPI generation runs this entry point through GetDocument.Insider without a database.
bool isOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

if (!isOpenApiGeneration)
{
    const int DatabaseMigrationTimeoutMinutes = 1;
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(DatabaseMigrationTimeoutMinutes));
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(
        app.Lifetime.ApplicationStopping,
        timeoutCts.Token);

    await app.Services.UseInfrastructureDatabaseAsync(cts.Token);
    await app.Services.SeedDatabaseAsync(cts.Token);
}

await app.RunAsync();
