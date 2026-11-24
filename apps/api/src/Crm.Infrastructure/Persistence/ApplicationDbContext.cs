using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context combining ASP.NET Core Identity tables and CRM domain entities.
/// </summary>
/// <param name="options">The database context configuration options.</param>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<
    AuthUser,
    AuthRole,
    Guid,
    IdentityUserClaim<Guid>,
    AuthUserRole,
    IdentityUserLogin<Guid>,
    AuthRoleClaim,
    IdentityUserToken<Guid>>(options)
{
    /// <summary>
    /// Configures global model conventions for the database context.
    /// </summary>
    /// <param name="configurationBuilder">The model configuration builder.</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.AddUtcDateTimeConventions();
    }

    /// <summary>
    /// Configures the entity mappings and database functions for the model.
    /// </summary>
    /// <param name="builder">The model builder used to construct the model for the context.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        builder.AddCustomDbFunctions();
    }
}
