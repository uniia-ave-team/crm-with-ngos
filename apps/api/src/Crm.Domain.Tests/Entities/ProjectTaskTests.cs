using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Shouldly;

namespace Crm.Domain.Tests.Entities;

/// <summary>
/// Unit tests for <see cref="ProjectTaskExtensions.ChangeStatus"/> covering the 5-status lifecycle,
/// automatic vs manual <see cref="ProjectTask.EndDate"/> handling, reopen rules, and the <c>EndDate &gt;= StartDate</c> invariant.
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
    public void ChangeStatusToDoneShouldNotAutoPopulateEndDate()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };

        task.ChangeStatus(ProjectTaskStatus.Done, requestedEndDate: null, UtcNow, updateEndDateWhenNull: false);

        task.Status.ShouldBe(ProjectTaskStatus.Done);
        task.EndDate.ShouldBeNull();
        task.EndDateWasAuto.ShouldBeFalse();
        task.UpdatedAt.ShouldBe(UtcNow);
    }

    [Fact]
    public void ChangeStatusToCompletedWithNullEndDateShouldAutoPopulateAndReopenShouldClearAutoEndDate()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.Done };

        task.ChangeStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow, updateEndDateWhenNull: false);

        task.Status.ShouldBe(ProjectTaskStatus.Completed);
        task.EndDate.ShouldBe(UtcNow);
        task.EndDateWasAuto.ShouldBeTrue();

        var reopenTime = UtcNow.AddHours(1);
        task.ChangeStatus(ProjectTaskStatus.InProgress, requestedEndDate: null, reopenTime, updateEndDateWhenNull: false);

        task.Status.ShouldBe(ProjectTaskStatus.InProgress);
        task.EndDate.ShouldBeNull();
        task.EndDateWasAuto.ShouldBeFalse();
        task.UpdatedAt.ShouldBe(reopenTime);
    }

    [Fact]
    public void ChangeStatusShouldPreserveManualEndDateOnDragAndDropAndOnReopen()
    {
        var plannedEndDate = StartDate.AddDays(10);
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.ToDo };

        task.ChangeStatus(ProjectTaskStatus.InProgress, plannedEndDate, UtcNow, updateEndDateWhenNull: true);
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();

        // Drag-and-drop PATCH with null endDate preserves manual EndDate
        task.ChangeStatus(ProjectTaskStatus.Done, requestedEndDate: null, UtcNow.AddHours(1), updateEndDateWhenNull: false);
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();

        // Complete with null endDate keeps manual EndDate
        task.ChangeStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow.AddHours(2), updateEndDateWhenNull: false);
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();

        // Reopen with null endDate keeps manual EndDate because EndDateWasAuto == false
        task.ChangeStatus(ProjectTaskStatus.InProgress, requestedEndDate: null, UtcNow.AddHours(3), updateEndDateWhenNull: false);
        task.EndDate.ShouldBe(plannedEndDate);
        task.EndDateWasAuto.ShouldBeFalse();
    }

    [Fact]
    public void ChangeStatusShouldThrowDomainValidationExceptionWhenExplicitOrPutAutoEndDateIsBeforeStartDateAndClampPatchAutoEndDate()
    {
        var task = new ProjectTask { StartDate = StartDate, Status = ProjectTaskStatus.InProgress };

        Should.Throw<InvalidTaskDateRangeException>(() =>
            task.ChangeStatus(ProjectTaskStatus.Completed, StartDate.AddSeconds(-1), UtcNow, updateEndDateWhenNull: false));

        var patchFutureTask = new ProjectTask { StartDate = UtcNow.AddDays(3), Status = ProjectTaskStatus.ToDo };
        patchFutureTask.ChangeStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow, updateEndDateWhenNull: false);

        patchFutureTask.Status.ShouldBe(ProjectTaskStatus.Completed);
        patchFutureTask.EndDate.ShouldBe(patchFutureTask.StartDate);
        patchFutureTask.EndDateWasAuto.ShouldBeTrue();

        var putFutureTask = new ProjectTask { StartDate = UtcNow.AddDays(3), Status = ProjectTaskStatus.ToDo };

        Should.Throw<InvalidTaskDateRangeException>(() =>
            putFutureTask.ChangeStatus(ProjectTaskStatus.Completed, requestedEndDate: null, UtcNow, updateEndDateWhenNull: true));
    }
}
