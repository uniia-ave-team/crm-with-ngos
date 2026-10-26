using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence.Seeders.SystemPresets;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds initial system preset table templates into the database using injected preset strategies.
/// </summary>
/// <param name="systemTableTemplates">The collection of system table template strategies.</param>
/// <param name="unitOfWork">The unit of work for managing database transactions.</param>
/// <param name="tableTemplateRepository">The repository used for accessing and persisting table templates.</param>
/// <param name="logger">The logger instance for tracking seeding execution.</param>
public partial class TableTemplateSeeder(
    IEnumerable<ISystemTableTemplate> systemTableTemplates,
    IUnitOfWork unitOfWork,
    ITableTemplateRepository tableTemplateRepository,
    ILogger<TableTemplateSeeder> logger) : IDatabaseSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        LogCheckingTableTemplates(logger);

        var existingNamesList = await tableTemplateRepository.GetSystemPresetNamesAsync(cancellationToken);

        var existingNames = existingNamesList.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var templatesToSeed = systemTableTemplates
            .Select(preset => preset.GetTemplate())
            .Where(template => !existingNames.Contains(template.Name))
            .ToList();

        if (templatesToSeed.Count == 0)
        {
            LogTableTemplatesAlreadySeeded(logger);
            return;
        }

        await tableTemplateRepository.CreateAsync(templatesToSeed, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogTableTemplatesSeededSuccessfully(logger, templatesToSeed.Count);
    }

    [LoggerMessage(EventId = LogEventIds.CheckingTableTemplates, Level = LogLevel.Information, Message = "Checking system preset table templates seeding status.")]
    private static partial void LogCheckingTableTemplates(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TableTemplatesAlreadySeeded, Level = LogLevel.Information, Message = "System preset table templates are already up to date.")]
    private static partial void LogTableTemplatesAlreadySeeded(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TableTemplatesSeededSuccessfully, Level = LogLevel.Information, Message = "Successfully seeded {SeededCount} new system preset table template(s).")]
    private static partial void LogTableTemplatesSeededSuccessfully(ILogger logger, int seededCount);
}
