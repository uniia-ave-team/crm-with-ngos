using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Unit tests verifying <see cref="SchemaSyncPlan"/> invariants and EF Core SQL translation
/// of <see cref="JsonbFunctionsExtensions.SyncJsonbKeys"/> to PostgreSQL <c>crm_sync_jsonb_keys</c>.
/// </summary>
public class JsonbFunctionsExtensionsTests
{
    [Fact]
    public void SchemaSyncPlanRequiresJsonbSyncShouldBeTrueOnlyWhenKeysToDropOrBackfillExist()
    {
        new SchemaSyncPlan([], []).RequiresJsonbSync.ShouldBeFalse();
        new SchemaSyncPlan(["f_removed"], []).RequiresJsonbSync.ShouldBeTrue();
        new SchemaSyncPlan([], ["f_required"]).RequiresJsonbSync.ShouldBeTrue();
        new SchemaSyncPlan(["f_removed"], ["f_required"]).RequiresJsonbSync.ShouldBeTrue();
    }

    [Fact]
    public void FieldSchemaSyncPlanShouldExposeStronglyTypedFieldsAndJsonbSyncRequirement()
    {
        var added = new CustomField { Name = "Added", IsRequired = true, Order = 0 };
        var removed = new CustomField { Name = "Removed", IsRequired = false, Order = 1 };

        var plan = new FieldSchemaSyncPlan<CustomField>(
            [added],
            [removed],
            [removed.Id.ToString()],
            [added.Id.ToString()]);

        plan.AddedFields.ShouldBe([added]);
        plan.RemovedFields.ShouldBe([removed]);
        plan.RequiresJsonbSync.ShouldBeTrue();
    }

    [Fact]
    public void SyncJsonbKeysShouldTranslateToPostgresCrmSyncJsonbKeysFunctionForCustomRecordsAndUsers()
    {
        using var context = CreateContext();
        string[] dropKeys = ["f_obsolete"];
        string[] backfillKeys = ["f_required_one", "f_required_two"];

        string customRecordSql = context.Set<CustomRecord>()
            .Select(r => JsonbFunctionsExtensions.SyncJsonbKeys(r.Data, dropKeys, backfillKeys))
            .ToQueryString();

        customRecordSql.ShouldContain(PostgresConstants.SyncJsonbKeysFunction);
        customRecordSql.ShouldContain("\"Data\"");

        string userSql = context.Set<User>()
            .Select(u => JsonbFunctionsExtensions.SyncJsonbKeys(u.CustomFields, dropKeys, backfillKeys))
            .ToQueryString();

        userSql.ShouldContain(PostgresConstants.SyncJsonbKeysFunction);
        userSql.ShouldContain("\"CustomFields\"");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }
}
