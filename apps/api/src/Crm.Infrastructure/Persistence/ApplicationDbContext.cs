using Crm.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

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
    /// Configures the entity mappings and seeds initial data for the model.
    /// </summary>
    /// <param name="builder">The modelBuilder used to construct the model for the context.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
