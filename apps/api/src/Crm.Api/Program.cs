using Crm.Api.Extensions;
using Crm.Infrastructure.Extensions;
using Crm.Infrastructure.Persistence.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureServices();

var app = builder.Build();

app.ConfigurePipeline();

using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
using var cts = CancellationTokenSource.CreateLinkedTokenSource(
    app.Lifetime.ApplicationStopping,
    timeoutCts.Token);

await app.Services.UseInfrastructureDatabaseAsync(cts.Token);
await app.Services.SeedDatabaseAsync(cts.Token);

await app.RunAsync();
