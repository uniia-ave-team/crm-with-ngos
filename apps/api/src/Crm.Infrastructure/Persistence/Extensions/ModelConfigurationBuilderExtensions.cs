using Crm.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides <see cref="ModelConfigurationBuilder"/> extension methods for global EF Core conventions.
/// </summary>
internal static class ModelConfigurationBuilderExtensions
{
    /// <summary>
    /// Registers global value converters ensuring all <see cref="DateTime"/> and <see cref="DateTimeOffset"/>
    /// properties across the entire model are persisted and materialized exclusively in UTC+0.
    /// </summary>
    /// <param name="configurationBuilder">The model configuration builder.</param>
    /// <returns>The same <see cref="ModelConfigurationBuilder"/> instance for chaining.</returns>
    public static ModelConfigurationBuilder AddUtcDateTimeConventions(
        this ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();

        return configurationBuilder;
    }
}
