using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Domain.ValueObjects;
using Crm.Infrastructure.Persistence.Seeders.SystemPresets;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Synchronizes system preset table templates into the database on startup,
/// inserting missing presets and updating existing ones in-place whenever template metadata
/// (<see cref="TableTemplate.Name"/>, <see cref="TableTemplate.Description"/>) or column definitions
/// (<see cref="TableTemplate.Fields"/>) differ from code presets.
/// </summary>
/// <param name="systemTableTemplates">The collection of system table template strategies.</param>
/// <param name="unitOfWork">The unit of work for managing database transactions.</param>
/// <param name="tableTemplateRepository">The repository used for accessing and persisting table templates.</param>
/// <param name="logger">The logger instance for tracking seeding execution.</param>
public sealed partial class TableTemplateSeeder(
    IEnumerable<ISystemTableTemplate> systemTableTemplates,
    IUnitOfWork unitOfWork,
    ITableTemplateRepository tableTemplateRepository,
    ILogger<TableTemplateSeeder> logger) : IDatabaseSeeder
{
    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        LogCheckingTableTemplates(logger);

        List<TableTemplate> existingPresets = await tableTemplateRepository.GetSystemPresetsForUpdateAsync(cancellationToken);
        HashSet<Guid> claimedIds = [];
        List<TableTemplate> templatesToCreate = [];
        int updatedCount = 0;

        foreach (ISystemTableTemplate preset in systemTableTemplates)
        {
            TableTemplate desired = preset.GetTemplate();
            desired.IsSystemPreset = true;
            desired.Fields.EnsureValidFieldSchema();

            List<TemplateFieldDefinition> normalizedFields = NormalizeFields(desired.Fields);
            desired.Fields = normalizedFields;

            TableTemplate? existing = FindMatchingPreset(existingPresets, claimedIds, desired, preset.LegacyNames);
            if (existing is null)
            {
                templatesToCreate.Add(desired);
                continue;
            }

            claimedIds.Add(existing.Id);

            if (!NeedsSync(existing, desired, normalizedFields))
            {
                continue;
            }

            existing.Name = desired.Name;
            existing.Description = desired.Description;
            existing.IsSystemPreset = true;
            existing.Fields.Clear();
            existing.Fields.AddRange(normalizedFields);
            updatedCount++;
        }

        if (templatesToCreate.Count == 0 && updatedCount == 0)
        {
            LogTableTemplatesAlreadySeeded(logger);
            return;
        }

        if (templatesToCreate.Count > 0)
        {
            await tableTemplateRepository.CreateAsync(templatesToCreate, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (templatesToCreate.Count > 0)
        {
            LogTableTemplatesSeededSuccessfully(logger, templatesToCreate.Count);
        }

        if (updatedCount > 0)
        {
            LogTableTemplatesUpdatedSuccessfully(logger, updatedCount);
        }
    }

    private static List<TemplateFieldDefinition> NormalizeFields(IEnumerable<TemplateFieldDefinition> fields)
        => [.. fields.OrderBy(f => f.Order)];

    private static TableTemplate? FindMatchingPreset(
        List<TableTemplate> existingPresets,
        HashSet<Guid> claimedIds,
        TableTemplate desired,
        IReadOnlyList<string> legacyNames)
    {
        TableTemplate? byName = existingPresets.FirstOrDefault(
            t => !claimedIds.Contains(t.Id)
                 && string.Equals(t.Name, desired.Name, StringComparison.OrdinalIgnoreCase));

        if (byName is not null)
        {
            return byName;
        }

        foreach (string legacyName in legacyNames)
        {
            if (string.IsNullOrWhiteSpace(legacyName))
            {
                continue;
            }

            TableTemplate? byLegacyName = existingPresets.FirstOrDefault(
                t => !claimedIds.Contains(t.Id)
                     && string.Equals(t.Name, legacyName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (byLegacyName is not null)
            {
                return byLegacyName;
            }
        }

        return null;
    }

    private static bool NeedsSync(
        TableTemplate existing,
        TableTemplate desired,
        List<TemplateFieldDefinition> normalizedDesiredFields)
        => !string.Equals(existing.Name, desired.Name, StringComparison.Ordinal)
           || !string.Equals(existing.Description, desired.Description, StringComparison.Ordinal)
           || !existing.IsSystemPreset
           || !existing.Fields.SequenceEqual(normalizedDesiredFields);

    [LoggerMessage(EventId = LogEventIds.CheckingTableTemplates, Level = LogLevel.Information, Message = "Checking system preset table templates seeding status.")]
    private static partial void LogCheckingTableTemplates(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TableTemplatesAlreadySeeded, Level = LogLevel.Information, Message = "System preset table templates are already up to date.")]
    private static partial void LogTableTemplatesAlreadySeeded(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TableTemplatesSeededSuccessfully, Level = LogLevel.Information, Message = "Successfully seeded {SeededCount} new system preset table template(s).")]
    private static partial void LogTableTemplatesSeededSuccessfully(ILogger logger, int seededCount);

    [LoggerMessage(EventId = LogEventIds.TableTemplatesUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully synchronized {UpdatedCount} existing system preset table template(s).")]
    private static partial void LogTableTemplatesUpdatedSuccessfully(ILogger logger, int updatedCount);
}
