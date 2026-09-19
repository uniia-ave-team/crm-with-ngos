using Crm.Api.Logging;
using Crm.Api.Middleware;
using Crm.Application.Extensions;
using Crm.Infrastructure.Extensions;
using Crm.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods to configure the application's hosting environment,
/// dependency injection container, and HTTP request pipeline.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Configures the application's services, integrating third-party libraries
    /// and internal architectural layers (Infrastructure and Application).
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> used to register services.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
    public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Host.AddSerilog();

        builder.Services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
        });

        builder.Services.AddControllers();

        builder.Services.AddCustomApiVersioning();

        builder.Services.AddHealthChecks();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddCustomRateLimiting(builder.Configuration);

        builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
        builder.Services.AddApplicationServices();

        return builder;
    }

    /// <summary>
    /// Configures the HTTP request pipeline, adding middleware for error handling,
    /// logging, routing, and security.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> representing the HTTP pipeline.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().WithDocumentPerVersion();
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("CRM API Documentation")
                       .WithTheme(ScalarTheme.DeepSpace);

                options.Authentication = new ScalarAuthenticationOptions
                {
                    PreferredSecuritySchemes = [JwtBearerDefaults.AuthenticationScheme],
                };
            });
        }
        else
        {
            app.UseHsts();
        }

        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, _, ex) => SerilogLogLevelResolver.Resolve(httpContext, ex);
        });

        app.UseHttpsRedirection();

        app.UseCors(CorsOptions.PolicyName);

        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers().RequireRateLimiting(RateLimitingExtensions.GlobalPolicyName);
        app.MapCustomHealthChecks();

        return app;
    }

    /// <summary>
    /// Configures Serilog as the primary logging provider, setting up console
    /// and file sinks with daily rolling intervals and log retention limits.
    /// </summary>
    /// <param name="host">The <see cref="ConfigureHostBuilder"/> to configure.</param>
    private static void AddSerilog(this ConfigureHostBuilder host)
    {
        host.UseSerilog((ctx, lc) =>
        {
            var serilogOptions = new SerilogOptions();
            ctx.Configuration.GetSection(SerilogOptions.Position).Bind(serilogOptions);

            string logsPath = Path.IsPathRooted(serilogOptions.LogPath)
                ? serilogOptions.LogPath
                : Path.Combine(AppContext.BaseDirectory, serilogOptions.LogPath);

            lc.ReadFrom.Configuration(ctx.Configuration)
              .WriteTo.Console()
              .WriteTo.File(
                  Path.Combine(logsPath, serilogOptions.AllLogsFileName),
                  rollingInterval: RollingInterval.Day,
                  shared: true,
                  retainedFileCountLimit: serilogOptions.RetainedFileCountLimit)
              .WriteTo.File(
                  Path.Combine(logsPath, serilogOptions.ErrorLogsFileName),
                  rollingInterval: RollingInterval.Day,
                  shared: true,
                  restrictedToMinimumLevel: LogEventLevel.Error,
                  retainedFileCountLimit: serilogOptions.RetainedErrorFileCountLimit);
        });
    }
}
