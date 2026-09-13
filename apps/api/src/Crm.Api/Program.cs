using Crm.Api.Extensions;
using Crm.Infrastructure.Extensions;
using Crm.Infrastructure.Persistence.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureServices();

var app = builder.Build();

app.ConfigurePipeline();

using var cts = CancellationTokenSource.CreateLinkedTokenSource(
    app.Lifetime.ApplicationStopping,
    new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

await app.Services.UseInfrastructureDatabaseAsync(cts.Token);
await app.Services.SeedDatabaseAsync(cts.Token);

await app.RunAsync();
