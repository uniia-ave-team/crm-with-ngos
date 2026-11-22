using Crm.Api.Consts;
using Crm.Api.Logging;
using Crm.Api.Middleware;
using Crm.Api.ModelBinding;
using Crm.Api.Options;
using Crm.Api.Serialization;
using Crm.Api.Services;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Crm.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using Serilog;

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
        builder.Host.AddCustomSerilog();

        builder.Services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
        });

        builder.Services.AddControllers(options =>
        {
            options.ModelBinderProviders.Insert(0, new UtcDateTimeModelBinderProvider());
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        });

        builder.Services.AddCustomApiVersioning();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.HttpContext.Response.Headers.XContentTypeOptions = SecurityHeaderValues.NoSniff;
            };
        });

        builder.Services.AddCustomSerilogOptions(builder.Configuration);
        builder.Services.AddCustomCors(builder.Configuration, builder.Environment);
        builder.Services.AddCustomRateLimiting(builder.Configuration);
        builder.Services.AddCustomForwardedHeaders(builder.Configuration);
        builder.Services.AddCustomAuthorization();

        builder.Services.AddSingleton<IAppVersionProvider, AssemblyAppVersionProvider>();

        builder.Services.AddInfrastructureServices(builder.Configuration);
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
        app.UseForwardedHeaders();
        app.UseSecurityHeaders();

        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, _, ex) => SerilogLogLevelResolver.Resolve(httpContext, ex);
        });

        app.UseExceptionHandler();

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

        app.UseHttpsRedirection();

        app.UseCors(AppCorsOptions.PolicyName);

        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers().RequireRateLimiting(RateLimitOptions.PolicyName);
        app.MapCustomHealthChecks();

        return app;
    }
}
