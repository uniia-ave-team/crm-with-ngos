using Crm.Application.Features.Projects.Queries;
using Crm.Application.Features.Projects.Validators;
using Crm.Application.Features.Tasks.Queries;
using Crm.Application.Features.Tasks.Validators;
using Shouldly;

namespace Crm.Application.Tests.Features.Projects.Validators;

/// <summary>
/// Unit tests for paged list query validators across projects, tags, topics, and tasks.
/// </summary>
public class ProjectQueryValidatorTests
{
    private readonly GetProjectsQueryValidator _projectsValidator = new();
    private readonly GetProjectTagsQueryValidator _tagsValidator = new();
    private readonly GetProjectTopicsQueryValidator _topicsValidator = new();
    private readonly GetProjectTasksQueryValidator _tasksValidator = new();

    [Fact]
    public void PagedQueriesWithDefaultParametersShouldPassValidation()
    {
        var projectId = Guid.NewGuid();

        _projectsValidator.Validate(new GetProjectsQuery()).IsValid.ShouldBeTrue();
        _tagsValidator.Validate(new GetProjectTagsQuery { ProjectId = projectId }).IsValid.ShouldBeTrue();
        _topicsValidator.Validate(new GetProjectTopicsQuery { ProjectId = projectId }).IsValid.ShouldBeTrue();
        _tasksValidator.Validate(new GetProjectTasksQuery { ProjectId = projectId }).IsValid.ShouldBeTrue();
        _tasksValidator.Validate(new GetProjectTasksQuery { ProjectId = null }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void PagedQueriesShouldEnforceStandardPaginationSearchSortAndProjectIdRules()
    {
        var projectId = Guid.NewGuid();

        _projectsValidator.Validate(new GetProjectsQuery(PageNumber: 0, PageSize: 999, SortOrder: "invalid")).IsValid.ShouldBeFalse();
        _tagsValidator.Validate(new GetProjectTagsQuery(SearchTerm: "a") { ProjectId = projectId }).IsValid.ShouldBeFalse();
        _topicsValidator.Validate(new GetProjectTopicsQuery { ProjectId = Guid.Empty }).IsValid.ShouldBeFalse();
        _tasksValidator.Validate(new GetProjectTasksQuery(PageSize: 0) { ProjectId = projectId }).IsValid.ShouldBeFalse();
        _tasksValidator.Validate(new GetProjectTasksQuery { ProjectId = Guid.Empty }).IsValid.ShouldBeFalse();
    }
}
