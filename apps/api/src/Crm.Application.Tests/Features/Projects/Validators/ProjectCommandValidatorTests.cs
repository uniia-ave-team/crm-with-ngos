using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Projects.Validators;
using Crm.Domain.Enums;
using Shouldly;

namespace Crm.Application.Tests.Features.Projects.Validators;

/// <summary>
/// Unit tests for <see cref="CreateProjectCommandValidator"/> and <see cref="UpdateProjectCommandValidator"/>.
/// </summary>
public class ProjectCommandValidatorTests
{
    private static readonly DateTime DefaultStartDate = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
    private readonly CreateProjectCommandValidator _createValidator = new();
    private readonly UpdateProjectCommandValidator _updateValidator = new();

    [Theory]
    [InlineData(ProjectVisibility.Private)]
    [InlineData(ProjectVisibility.PublicViewPrivateEdit)]
    [InlineData(ProjectVisibility.Public)]
    public void CreateProjectValidatorWhenValidVisibilityShouldPass(ProjectVisibility visibility)
    {
        var command = new CreateProjectCommand("CRM Roadmap", "Description", visibility, DefaultStartDate);

        var result = _createValidator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreateProjectValidatorWhenVisibilityIsOutOfRangeShouldFail()
    {
        var command = new CreateProjectCommand(
            "Secret Project",
            "Description",
            (ProjectVisibility)99,
            DefaultStartDate);

        var result = _createValidator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateProjectCommand.Visibility));
    }

    [Fact]
    public void CreateProjectValidatorWhenStartDateMissingOrEndDateEarlierThanStartDateShouldFail()
    {
        var missingStart = new CreateProjectCommand("Project", null, ProjectVisibility.Public, default);
        _createValidator.Validate(missingStart).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateProjectCommand.StartDate));

        var invalidRange = new CreateProjectCommand(
            "Project",
            null,
            ProjectVisibility.Public,
            DefaultStartDate,
            DefaultStartDate.AddDays(-1));
        _createValidator.Validate(invalidRange).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateProjectCommand.EndDate));
    }

    [Theory]
    [InlineData(ProjectVisibility.Private)]
    [InlineData(ProjectVisibility.PublicViewPrivateEdit)]
    [InlineData(ProjectVisibility.Public)]
    public void UpdateProjectValidatorWhenValidVisibilityShouldPass(ProjectVisibility visibility)
    {
        var command = new UpdateProjectCommand("CRM Roadmap", null, visibility, DefaultStartDate)
        {
            Id = Guid.NewGuid(),
        };

        var result = _updateValidator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpdateProjectValidatorWhenVisibilityIsOutOfRangeShouldFail()
    {
        var command = new UpdateProjectCommand(
            "Secret Project",
            null,
            (ProjectVisibility)99,
            DefaultStartDate)
        {
            Id = Guid.NewGuid(),
        };

        var result = _updateValidator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateProjectCommand.Visibility));
    }

    [Fact]
    public void UpdateProjectValidatorWhenStartDateMissingOrEndDateEarlierThanStartDateShouldFail()
    {
        var missingStart = new UpdateProjectCommand("Project", null, ProjectVisibility.Public, default)
        {
            Id = Guid.NewGuid(),
        };
        _updateValidator.Validate(missingStart).Errors
            .ShouldContain(e => e.PropertyName == nameof(UpdateProjectCommand.StartDate));

        var invalidRange = new UpdateProjectCommand(
            "Project",
            null,
            ProjectVisibility.Public,
            DefaultStartDate,
            DefaultStartDate.AddDays(-1))
        {
            Id = Guid.NewGuid(),
        };
        _updateValidator.Validate(invalidRange).Errors
            .ShouldContain(e => e.PropertyName == nameof(UpdateProjectCommand.EndDate));
    }

    [Fact]
    public void ProjectValidatorsShouldRejectNullCharacters()
    {
        var createWithNullTitle = new CreateProjectCommand("Bad\0Project", null, ProjectVisibility.Public, DefaultStartDate);
        _createValidator.Validate(createWithNullTitle).IsValid.ShouldBeFalse();

        var createWithNullDesc = new CreateProjectCommand("Project", "Bad\0Desc", ProjectVisibility.Public, DefaultStartDate);
        _createValidator.Validate(createWithNullDesc).IsValid.ShouldBeFalse();
    }
}
