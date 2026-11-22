using System.Text.Json.Nodes;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Tasks.Queries;
using Crm.Application.Features.Tasks.Validators;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Enums;
using Shouldly;

namespace Crm.Application.Tests.Features.Tasks.Validators;

/// <summary>
/// Unit tests for <see cref="CreateTaskCommandValidator"/>, <see cref="UpdateTaskCommandValidator"/>,
/// <see cref="UpdateTaskStatusCommandValidator"/>, and <see cref="UploadTaskAttachmentCommandValidator"/>.
/// </summary>
public class TaskCommandValidatorTests
{
    private static readonly DateTime DefaultStartDate = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
    private readonly CreateTaskCommandValidator _createValidator = new();
    private readonly UpdateTaskCommandValidator _updateValidator = new();
    private readonly UpdateTaskStatusCommandValidator _updateStatusValidator = new();
    private readonly UploadTaskAttachmentCommandValidator _uploadAttachmentValidator = new();

    [Fact]
    public void CreateTaskValidatorWhenTopicIdIsEmptyShouldFail()
    {
        var command = new CreateTaskCommand(Guid.Empty, "Task Title", [], [], null, DefaultStartDate)
        {
            ProjectId = Guid.NewGuid(),
        };

        var result = _createValidator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateTaskCommand.TopicId));
    }

    [Fact]
    public void CreateTaskValidatorShouldValidateExternalLinksAndMetadataLimits()
    {
        var validCommand = new CreateTaskCommand(
            Guid.NewGuid(),
            "Task Title",
            [],
            [],
            null,
            DefaultStartDate,
            ProjectTaskStatus.Done,
            TaskPriority.High,
            ExternalLinks: [new TaskExternalLinkDto("Spec", "https://example.org/spec")],
            Metadata: new JsonObject { ["grantCode"] = "NGO-2026", ["budget"] = 1200 })
        {
            ProjectId = Guid.NewGuid(),
        };
        _createValidator.Validate(validCommand).IsValid.ShouldBeTrue();

        var invalidSchemeLink = validCommand with
        {
            ExternalLinks = [new TaskExternalLinkDto("XSS", "javascript:alert(1)")],
        };
        _createValidator.Validate(invalidSchemeLink).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateTaskCommand.ExternalLinks));

        var tooDeepMetadata = validCommand with
        {
            Metadata = new JsonObject
            {
                ["a"] = new JsonObject
                {
                    ["b"] = new JsonObject
                    {
                        ["c"] = new JsonObject
                        {
                            ["d"] = new JsonObject
                            {
                                ["e"] = "too-deep",
                            },
                        },
                    },
                },
            },
        };
        _createValidator.Validate(tooDeepMetadata).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateTaskCommand.Metadata));
    }

    [Fact]
    public void UpdateTaskValidatorWhenTopicIdIsEmptyShouldFail()
    {
        var command = new UpdateTaskCommand(
            Guid.Empty,
            "Task Title",
            [],
            [],
            null,
            DefaultStartDate,
            ProjectTaskStatus.InProgress,
            TaskPriority.Medium)
        {
            ProjectId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
        };

        var result = _updateValidator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateTaskCommand.TopicId));
    }

    [Fact]
    public void UpdateTaskValidatorWhenValidShouldPass()
    {
        var command = new UpdateTaskCommand(
            Guid.NewGuid(),
            "Task Title",
            [],
            [],
            null,
            DefaultStartDate,
            ProjectTaskStatus.Done,
            TaskPriority.High)
        {
            ProjectId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
        };

        _updateValidator.Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpdateTaskStatusValidatorShouldAllowNullTopicIdButRejectEmptyGuid()
    {
        var withoutTopic = new UpdateTaskStatusCommand(ProjectTaskStatus.Done)
        {
            ProjectId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
        };
        _updateStatusValidator.Validate(withoutTopic).IsValid.ShouldBeTrue();

        var withValidTopic = new UpdateTaskStatusCommand(ProjectTaskStatus.InProgress, Guid.NewGuid())
        {
            ProjectId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
        };
        _updateStatusValidator.Validate(withValidTopic).IsValid.ShouldBeTrue();

        var withEmptyTopic = new UpdateTaskStatusCommand(ProjectTaskStatus.InProgress, Guid.Empty)
        {
            ProjectId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
        };
        var result = _updateStatusValidator.Validate(withEmptyTopic);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateTaskStatusCommand.TopicId));
    }

    [Theory]
    [InlineData("xss.html", "text/html")]
    [InlineData("page.htm", "text/html")]
    [InlineData("vector.svg", "image/svg+xml")]
    [InlineData("script.js", "application/javascript")]
    [InlineData("payload.exe", "application/octet-stream")]
    [InlineData("noextension", "text/plain")]
    public void UploadTaskAttachmentValidatorShouldRejectDangerousOrDisallowedExtensions(string fileName, string contentType)
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadTaskAttachmentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            stream,
            fileName,
            contentType,
            stream.Length,
            Guid.NewGuid());

        var result = _uploadAttachmentValidator.Validate(command);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UploadTaskAttachmentCommand.OriginalFileName));
    }

    [Fact]
    public void UploadTaskAttachmentValidatorShouldRejectMismatchedContentTypeAndOversizedFile()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var validCommand = new UploadTaskAttachmentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            stream,
            "report.pdf",
            "application/pdf",
            stream.Length,
            Guid.NewGuid());
        _uploadAttachmentValidator.Validate(validCommand).IsValid.ShouldBeTrue();

        var fallbackCommand = validCommand with { ContentType = "application/octet-stream" };
        _uploadAttachmentValidator.Validate(fallbackCommand).IsValid.ShouldBeTrue();

        var mismatchedMimeCommand = validCommand with { ContentType = "text/html" };
        var mimeResult = _uploadAttachmentValidator.Validate(mismatchedMimeCommand);
        mimeResult.IsValid.ShouldBeFalse();
        mimeResult.Errors.ShouldContain(e => e.PropertyName == nameof(UploadTaskAttachmentCommand.ContentType));

        var exactLimitCommand = validCommand with { FileSize = TaskValidationConstants.MaxAttachmentFileSize };
        _uploadAttachmentValidator.Validate(exactLimitCommand).IsValid.ShouldBeTrue();

        var windowsPathCommand = validCommand with { OriginalFileName = @"C:\fakepath\report.pdf" };
        _uploadAttachmentValidator.Validate(windowsPathCommand).IsValid.ShouldBeTrue();

        var unixPathDangerousCommand = validCommand with
        {
            OriginalFileName = "/tmp/payload.exe",
            ContentType = "application/octet-stream",
        };
        _uploadAttachmentValidator.Validate(unixPathDangerousCommand).IsValid.ShouldBeFalse();

        var oversizedCommand = validCommand with
        {
            FileSize = TaskValidationConstants.MaxAttachmentFileSize + 1,
        };
        var sizeResult = _uploadAttachmentValidator.Validate(oversizedCommand);
        sizeResult.IsValid.ShouldBeFalse();
        sizeResult.Errors.ShouldContain(e => e.PropertyName == nameof(UploadTaskAttachmentCommand.FileSize));
    }

    [Theory]
    [InlineData("notes.md", "text/x-markdown")]
    [InlineData("notes.md", "text/markdown")]
    [InlineData("export.csv", "application/csv")]
    [InlineData("export.csv", "text/csv")]
    public void UploadTaskAttachmentValidatorShouldAcceptCommonMarkdownAndCsvMimeTypes(string fileName, string contentType)
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadTaskAttachmentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            stream,
            fileName,
            contentType,
            stream.Length,
            Guid.NewGuid());

        _uploadAttachmentValidator.Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TaskAndCommentValidatorsShouldRejectNullCharactersInTextAndJsonFields()
    {
        var baseCmd = new CreateTaskCommand(
            Guid.NewGuid(),
            "Valid Title",
            [],
            [],
            "Valid description",
            DefaultStartDate,
            ProjectTaskStatus.ToDo,
            TaskPriority.Medium)
        {
            ProjectId = Guid.NewGuid(),
        };

        _createValidator.Validate(baseCmd with { Title = "Bad\0Title" }).IsValid.ShouldBeFalse();
        _createValidator.Validate(baseCmd with { Description = "Bad\0Desc" }).IsValid.ShouldBeFalse();
        _createValidator.Validate(baseCmd with
        {
            ExternalLinks = [new TaskExternalLinkDto("Bad\0Link", "https://example.org")],
        }).IsValid.ShouldBeFalse();
        _createValidator.Validate(baseCmd with
        {
            Metadata = new JsonObject { ["key"] = "bad\0value" },
        }).IsValid.ShouldBeFalse();
        _createValidator.Validate(baseCmd with
        {
            Metadata = new JsonObject { ["bad\0key"] = "ok" },
        }).IsValid.ShouldBeFalse();

        var commentValidator = new CreateTaskCommentCommandValidator();
        commentValidator.Validate(new CreateTaskCommentCommand("Bad\0comment")
        {
            ProjectId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
        }).IsValid.ShouldBeFalse();

        using var stream = new MemoryStream([1, 2, 3]);
        _uploadAttachmentValidator.Validate(new UploadTaskAttachmentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            stream,
            "bad\0.pdf",
            "application/pdf",
            3,
            Guid.NewGuid())).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void GetTaskCommentsQueryValidatorShouldValidatePaginationBounds()
    {
        var validator = new GetTaskCommentsQueryValidator();

        var valid = new GetTaskCommentsQuery(PageNumber: 1, PageSize: 20)
        {
            ProjectId = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
        };
        validator.Validate(valid).IsValid.ShouldBeTrue();

        var invalidPage = valid with { PageNumber = 0 };
        validator.Validate(invalidPage).IsValid.ShouldBeFalse();

        var invalidSize = valid with { PageSize = 0 };
        validator.Validate(invalidSize).IsValid.ShouldBeFalse();
    }
}
