using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services.Storage;
using Crm.Infrastructure.Services.Storage.FileValidators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Infrastructure.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for local file storage, quota tracking,
/// transactional file coordination, and image format validators.
/// </summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FileStorageOptions"/>, local disk storage services, quota tracking, and image validators.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddFileStorageServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.Position))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ILocalStorageQuotaTracker, LocalStorageQuotaTracker>();
        services.AddSingleton<IFileUrlProvider, LocalFileUrlProvider>();
        services.AddSingleton<IFormatValidator, SvgImageValidator>();
        services.AddSingleton<IFormatValidator, RasterImageValidator>();
        services.AddSingleton<IImageValidator, ImageValidator>();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IFileTransactionTracker, FileTransactionTracker>();

        return services;
    }
}
