using System.Text.Json.Nodes;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.ValueObjects;
using Shouldly;

namespace Crm.Domain.Tests.Entities;

/// <summary>
/// Unit tests for <see cref="ProjectTask"/> status transitions and domain invariants,
/// covering the 5-status lifecycle, automatic vs manual <see cref="ProjectTask.EndDate"/> handling,
/// reopen rules, the <c>EndDate &gt;= StartDate</c> invariant, and <see cref="TaskExternalLink"/> equality.
/// </summary>
public class ProjectTaskTests
{
    private static readonly DateTime StartDate = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UtcNow = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EnumShouldContainAllFiveRequiredStatusesInOrder()
    {
        Enum.GetValues<ProjectTaskStatus>().ShouldBe(
        [
            ProjectTaskStatus.ToDo,
            ProjectTaskStatus.InProgress,
            ProjectTaskStatus.Blocked,
            ProjectTaskStatus.Done,
            ProjectTaskStatus.Completed,
        ]);

        ((byte)ProjectTaskStatus.ToDo).ShouldBe((byte)0);
        ((byte)ProjectTaskStatus.InProgress).ShouldBe((byte)1);
        ((byte)ProjectTaskStatus.Blocked).ShouldBe((byte)2);
        ((byte)ProjectTaskStatus.Done).ShouldBe((byte)3);
        ((byte)ProjectTaskStatus.Completed).ShouldBe((byte)4);
    }

    [Fact]
    public void TransitionStatusToDoneShouldNotAutoPopulateEndDate()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };

        task.TransitionStatus(ProjectTaskStatus.Done, requestedEndDate: null, UtcNow);

        task.Status.ShouldBe(ProjectTaskStatus.Done);
        task.EndDate.ShouldBeNull();
        task.EndDateWasAuto.ShouldBeFalse();
        task.UpdatedAt.ShouldBe(UtcNow);
    }

    [Fact]
    public void TransitionStatusToCompletedWithNullEndDateShouldAutoPopulateAndReopenShouldClearAutoEndDate()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.Done };

        task.TransitionStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow);

        task.Status.ShouldBe(ProjectTaskStatus.Completed);
        task.EndDate.ShouldBe(UtcNow);
        task.EndDateWasAuto.ShouldBeTrue();

        var reopenTime = UtcNow.AddHours(1);
        task.TransitionStatus(ProjectTaskStatus.InProgress, requestedEndDate: null, reopenTime);

        task.Status.ShouldBe(ProjectTaskStatus.InProgress);
        task.EndDate.ShouldBeNull();
        task.EndDateWasAuto.ShouldBeFalse();
        task.UpdatedAt.ShouldBe(reopenTime);
    }

    [Fact]
    public void TransitionStatusShouldPreserveManualEndDateOnDragAndDropAndOnReopen()
    {
        var plannedEndDate = StartDate.AddDays(10);
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.ToDo };

        task.ApplyFullStatusUpdate(ProjectTaskStatus.InProgress, plannedEndDate, UtcNow);
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();

        // Drag-and-drop PATCH with null endDate preserves manual EndDate
        task.TransitionStatus(ProjectTaskStatus.Done, requestedEndDate: null, UtcNow.AddHours(1));
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();

        // Complete with null endDate keeps manual EndDate
        task.TransitionStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow.AddHours(2));
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();

        // Reopen with null endDate keeps manual EndDate because EndDateWasAuto == false
        task.TransitionStatus(ProjectTaskStatus.InProgress, requestedEndDate: null, UtcNow.AddHours(3));
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();
    }

    [Fact]
    public void StatusUpdateShouldThrowDomainValidationExceptionWhenExplicitOrPutAutoEndDateIsBeforeStartDateAndClampPatchAutoEndDate()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };

        Should.Throw<InvalidDateRangeException>(() =>
            task.TransitionStatus(ProjectTaskStatus.Completed, StartDate.AddSeconds(-1), UtcNow));

        var patchFutureTask = new ProjectTask { StartDate = UtcNow.AddDays(3), Status = ProjectTaskStatus.ToDo };
        patchFutureTask.TransitionStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow);

        patchFutureTask.Status.ShouldBe(ProjectTaskStatus.Completed);
        patchFutureTask.EndDate.ShouldBe(patchFutureTask.StartDate);
        patchFutureTask.EndDateWasAuto.ShouldBeTrue();

        var putFutureTask = new ProjectTask { StartDate = UtcNow.AddDays(3), Status = ProjectTaskStatus.ToDo };

        Should.Throw<InvalidDateRangeException>(() =>
            putFutureTask.ApplyFullStatusUpdate(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow));
    }

    [Fact]
    public void ApplyFullStatusUpdateWithUnchangedAutoEndDateShouldPreserveAutoFlagAndClearOnReopen()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };

        // Auto-complete via PATCH
        task.TransitionStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow);
        task.EndDate.ShouldBe(UtcNow);
        task.EndDateWasAuto.ShouldBeTrue();

        // Full edit via PUT echoing back the unchanged auto EndDate preserves EndDateWasAuto
        var editTime = UtcNow.AddMinutes(10);
        task.ApplyFullStatusUpdate(ProjectTaskStatus.Completed, requestedEndDate: UtcNow, editTime);
        task.EndDate.ShouldBe(UtcNow);
        task.EndDateWasAuto.ShouldBeTrue();

        // Reopen via PATCH still clears the auto-populated EndDate
        var reopenTime = UtcNow.AddMinutes(20);
        task.TransitionStatus(ProjectTaskStatus.InProgress, requestedEndDate: null, reopenTime);
        task.EndDate.ShouldBeNull();
        task.EndDateWasAuto.ShouldBeFalse();
    }

    [Fact]
    public void ApplyFullStatusUpdateWithJsMillisecondTruncatedAutoEndDateShouldPreserveAutoFlagAndClearOnReopen()
    {
        var microsecondAutoEndDate = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc)
            .AddMilliseconds(123)
            .AddMicroseconds(456);
        var jsTruncatedEndDate = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc)
            .AddMilliseconds(123);

        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };

        // Auto-complete via PATCH with PostgreSQL microsecond precision
        task.TransitionStatus(ProjectTaskStatus.Completed, requestedEndDate: null, microsecondAutoEndDate);
        task.EndDate.ShouldBe(microsecondAutoEndDate);
        task.EndDateWasAuto.ShouldBeTrue();

        // Full edit via PUT echoing back JS Date-truncated millisecond timestamp preserves EndDateWasAuto
        task.ApplyFullStatusUpdate(ProjectTaskStatus.Completed, requestedEndDate: jsTruncatedEndDate, microsecondAutoEndDate.AddMinutes(5));
        task.EndDate.ShouldBe(microsecondAutoEndDate);
        task.EndDateWasAuto.ShouldBeTrue();

        // Reopen via PATCH still clears the auto-populated EndDate
        task.TransitionStatus(ProjectTaskStatus.InProgress, requestedEndDate: null, microsecondAutoEndDate.AddMinutes(10));
        task.EndDate.ShouldBeNull();
        task.EndDateWasAuto.ShouldBeFalse();
    }

    [Fact]
    public void ApplyFullStatusUpdateWithModifiedEndDateShouldTreatItAsManual()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };
        task.TransitionStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow);
        task.EndDateWasAuto.ShouldBeTrue();

        var customEndDate = UtcNow.AddDays(1);
        task.ApplyFullStatusUpdate(ProjectTaskStatus.Completed, requestedEndDate: customEndDate, UtcNow.AddHours(1));
        task.EndDate.ShouldBe(customEndDate);
        task.EndDateWasAuto.ShouldBeFalse();
    }

    [Fact]
    public void ApplyFullStatusUpdateWhenAlreadyCompletedAndStartDateMovesPastEndDateShouldThrow()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };
        task.TransitionStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow);

        // Simulate PUT moving StartDate after existing EndDate while passing null EndDate
        task.StartDate = UtcNow.AddDays(5);

        Should.Throw<InvalidDateRangeException>(() =>
            task.ApplyFullStatusUpdate(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow.AddHours(1)));
    }

    [Fact]
    public void StatusTransitionWithUndefinedEnumValueShouldThrowArgumentOutOfRangeException()
    {
        var task = new ProjectTask { StartDate = StartDate };

        Should.Throw<ArgumentOutOfRangeException>(() =>
            task.TransitionStatus((ProjectTaskStatus)99, requestedEndDate: null, UtcNow));
    }

    [Fact]
    public void TaskExternalLinkShouldSupportValueEqualityAndNormalizeFields()
    {
        var linkA = new TaskExternalLink("  https://example.org/spec  ", "  Spec  ");
        var linkB = new TaskExternalLink("https://example.org/spec", "Spec");
        var linkNoTitle = new TaskExternalLink("https://example.org/spec", "   ");

        linkA.ShouldBe(linkB);
        linkA.Url.ShouldBe("https://example.org/spec");
        linkA.Title.ShouldBe("Spec");
        linkNoTitle.Title.ShouldBeNull();
    }

    [Fact]
    public void MaintenanceNoticeUpdateShouldEnforceWindowInvariantAndNormalizeToUtc()
    {
        Should.Throw<InvalidMaintenanceWindowException>(() =>
            new MaintenanceNotice().Update("Scheduled update", UtcNow, UtcNow, UtcNow));

        var unspecifiedStart = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Unspecified);
        var unspecifiedEnd = new DateTime(2026, 11, 16, 13, 0, 0, DateTimeKind.Unspecified);
        var notice = new MaintenanceNotice().Update("  Scheduled update  ", unspecifiedStart, unspecifiedEnd, UtcNow);

        notice.Id.ShouldBe(MaintenanceNotice.SingletonId);
        notice.Message.ShouldBe("Scheduled update");
        notice.StartsAt.Kind.ShouldBe(DateTimeKind.Utc);
        notice.EndsAt.Kind.ShouldBe(DateTimeKind.Utc);
        notice.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ProjectEnsureValidDateRangeShouldThrowWhenEndDatePrecedesStartDateAndNormalizeToUtc()
    {
        var project = new Project
        {
            StartDate = StartDate,
            EndDate = StartDate.AddMinutes(-1),
        };

        Should.Throw<InvalidDateRangeException>(project.EnsureValidDateRange);

        var validProject = new Project
        {
            StartDate = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified),
            EndDate = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Unspecified),
        };
        validProject.EnsureValidDateRange();
        validProject.StartDate.Kind.ShouldBe(DateTimeKind.Utc);
        validProject.EndDate.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void DateTimeExtensionsShouldNormalizeDateTimeAndDateTimeOffsetToUtcZero()
    {
        var unspecified = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Unspecified);
        unspecified.ToUtc().Kind.ShouldBe(DateTimeKind.Utc);
        unspecified.ToUtc().Hour.ShouldBe(10);

        var offsetPlusTwo = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.FromHours(2));
        var normalizedOffset = offsetPlusTwo.ToUtc();
        normalizedOffset.Offset.ShouldBe(TimeSpan.Zero);
        normalizedOffset.Hour.ShouldBe(10);

        DateTime? nullDt = null;
        nullDt.ToUtc().ShouldBeNull();

        DateTimeOffset? nullDto = null;
        nullDto.ToUtc().ShouldBeNull();
    }

    [Fact]
    public void NormalizableTitleExtensionsShouldTrimAndNormalizeTitlesForTagAndTopic()
    {
        var tag = new Tag().SetTitle("  Urgent  ");
        tag.Title.ShouldBe("Urgent");
        tag.NormalizedTitle.ShouldBe("URGENT");

        var topic = new Topic { Title = "  Backend  " }.SyncNormalizedTitle();
        topic.Title.ShouldBe("Backend");
        topic.NormalizedTitle.ShouldBe("BACKEND");
    }

    [Fact]
    public void JsonObjectValidateAndBackfillShouldPreserveStringValuesAndRejectNonStringValues()
    {
        var eventField = new CustomField { Name = "Event At", IsRequired = true, Order = 0 };
        string eventKey = eventField.Id.ToString();
        var fields = new List<CustomField> { eventField };
        var payload = new JsonObject
        {
            [eventKey] = "2026-11-21T12:00:00.0000000+02:00",
        };

        var validated = payload.ValidateAndBackfillPayload(fields);

        validated[eventKey]!.GetValue<string>().ShouldBe("2026-11-21T12:00:00.0000000+02:00");

        var invalidPayload = new JsonObject
        {
            [eventKey] = 42,
        };

        Should.Throw<InvalidCustomRecordPayloadException>(() => invalidPayload.ValidateAndBackfillPayload(fields));
    }
}
