using Crm.Api.Extensions;
using Crm.Infrastructure.Extensions;
using Crm.Infrastructure.Persistence.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureServices();

var app = builder.Build();

app.ConfigurePipeline();

const int DatabaseMigrationTimeoutMinutes = 1;
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(DatabaseMigrationTimeoutMinutes));
using var cts = CancellationTokenSource.CreateLinkedTokenSource(
    app.Lifetime.ApplicationStopping,
    timeoutCts.Token);

await app.Services.UseInfrastructureDatabaseAsync(cts.Token);
await app.Services.SeedDatabaseAsync(cts.Token);

await app.RunAsync();
