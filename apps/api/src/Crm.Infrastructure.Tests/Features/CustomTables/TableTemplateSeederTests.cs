using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Domain.ValueObjects;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Seeders;
using Crm.Infrastructure.Persistence.Seeders.SystemPresets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Unit tests verifying idempotent creation and automatic in-place synchronization of system preset table templates,
/// including column additions, removals, renames, ordering changes, and legacy name matching without explicit preset IDs.
/// </summary>
public class TableTemplateSeederTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITableTemplateRepository _repository = Substitute.For<ITableTemplateRepository>();

    [Fact]
    public async Task SeedAsyncWhenPresetIsMissingShouldCreateWithoutExplicitIdAndSave()
    {
        _repository.GetSystemPresetsForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        List<TableTemplate>? createdCapture = null;
        _repository.CreateAsync(
                Arg.Do<IEnumerable<TableTemplate>>(t => createdCapture = [.. t]),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var seeder = new TableTemplateSeeder(
            [new DonorBaseSystemTableTemplate()],
            _unitOfWork,
            _repository,
            NullLogger<TableTemplateSeeder>.Instance);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        createdCapture.ShouldNotBeNull();
        var created = createdCapture.ShouldHaveSingleItem();
        created.Id.ShouldBe(Guid.Empty);
        created.Name.ShouldBe("База донорів");
        created.IsSystemPreset.ShouldBeTrue();
        created.Fields.Count.ShouldBe(5);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsyncWhenExistingMatchesPresetExactlyShouldNotSave()
    {
        var preset = new DonorBaseSystemTableTemplate();
        TableTemplate existing = preset.GetTemplate();

        _repository.GetSystemPresetsForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns([existing]);

        var seeder = new TableTemplateSeeder(
            [preset],
            _unitOfWork,
            _repository,
            NullLogger<TableTemplateSeeder>.Instance);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<IEnumerable<TableTemplate>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsyncWhenColumnNameRequirednessOrderOrCountDiffersShouldUpdateInPlaceKeepingExistingId()
    {
        Guid existingDbId = Guid.CreateVersion7();
        var existing = new TableTemplate
        {
            Id = existingDbId,
            Name = "База донорів",
            Description = "Old description",
            IsSystemPreset = true,
            Fields =
            [
                new TemplateFieldDefinition("Назва", isRequired: true, order: 0),
                new TemplateFieldDefinition("Старе поле", isRequired: false, order: 1),
            ],
        };

        _repository.GetSystemPresetsForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns([existing]);

        var seeder = new TableTemplateSeeder(
            [new DonorBaseSystemTableTemplate()],
            _unitOfWork,
            _repository,
            NullLogger<TableTemplateSeeder>.Instance);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        existing.Id.ShouldBe(existingDbId);
        existing.Description.ShouldBe("Стандартний шаблон для ведення обліку грантодавців, донорських організацій та програм підтримки.");
        existing.Fields.Count.ShouldBe(5);
        existing.Fields[0].ShouldBe(new TemplateFieldDefinition("Назва", true, 0));
        existing.Fields[4].ShouldBe(new TemplateFieldDefinition("Контакти", false, 4));
        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<IEnumerable<TableTemplate>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsyncWhenPresetNameChangesShouldMatchByLegacyNamesAndRenameInPlace()
    {
        Guid existingDbId = Guid.CreateVersion7();
        var existingByLegacyName = new TableTemplate
        {
            Id = existingDbId,
            Name = "Legacy Template Name",
            IsSystemPreset = true,
            Fields = [new TemplateFieldDefinition("Col A", false, 0)],
        };

        _repository.GetSystemPresetsForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns([existingByLegacyName]);

        ISystemTableTemplate renamedPresetByLegacy = new StubSystemTemplate(
            new TableTemplate
            {
                Name = "Modern Template Name",
                Description = "Updated description",
                IsSystemPreset = true,
                Fields =
                [
                    new TemplateFieldDefinition("Col A", true, 0),
                    new TemplateFieldDefinition("Col B", false, 1),
                ],
            },
            ["Legacy Template Name"]);

        var seeder = new TableTemplateSeeder(
            [renamedPresetByLegacy],
            _unitOfWork,
            _repository,
            NullLogger<TableTemplateSeeder>.Instance);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        existingByLegacyName.Id.ShouldBe(existingDbId);
        existingByLegacyName.Name.ShouldBe("Modern Template Name");
        existingByLegacyName.Description.ShouldBe("Updated description");
        existingByLegacyName.Fields.Count.ShouldBe(2);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsyncWhenFieldsSingleCharacterDiffersShouldStillDetectAndSync()
    {
        var preset = new DonorBaseSystemTableTemplate();
        TableTemplate existing = preset.GetTemplate();
        existing.Fields[2] = new TemplateFieldDefinition("особливості подання", isRequired: false, order: 2);

        _repository.GetSystemPresetsForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns([existing]);

        var seeder = new TableTemplateSeeder(
            [preset],
            _unitOfWork,
            _repository,
            NullLogger<TableTemplateSeeder>.Instance);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        existing.Fields[2].Name.ShouldBe("Особливості подання");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsyncShouldTriggerEfCoreChangeTrackingForJsonbOwnedFields()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        await using var context = new ApplicationDbContext(options);

        var trackedExisting = new TableTemplate
        {
            Id = Guid.CreateVersion7(),
            Name = "База донорів",
            Description = "Old",
            IsSystemPreset = true,
            Fields = [new TemplateFieldDefinition("Стара колонка", true, 0)],
        };

        context.Attach(trackedExisting);
        context.ChangeTracker.HasChanges().ShouldBeFalse();

        _repository.GetSystemPresetsForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns([trackedExisting]);

        var seeder = new TableTemplateSeeder(
            [new DonorBaseSystemTableTemplate()],
            _unitOfWork,
            _repository,
            NullLogger<TableTemplateSeeder>.Instance);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.DetectChanges();
        context.ChangeTracker.HasChanges().ShouldBeTrue();
        trackedExisting.Fields.Count.ShouldBe(5);
    }

    private sealed class StubSystemTemplate(
        TableTemplate template,
        IReadOnlyList<string>? legacyNames = null) : ISystemTableTemplate
    {
        public IReadOnlyList<string> LegacyNames => legacyNames ?? [];

        public TableTemplate GetTemplate() => template;
    }
}
