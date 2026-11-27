using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Tasks.Mappings;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Tasks.Commands;

/// <summary>
/// Unit tests for <see cref="UploadTaskAttachmentCommandHandler"/> and <see cref="DeleteTaskAttachmentCommandHandler"/>.
/// </summary>
public class TaskAttachmentCommandHandlerTests
{
    private readonly IProjectTaskRepository _taskRepository = Substitute.For<IProjectTaskRepository>();
    private readonly ITaskAttachmentRepository _attachmentRepository = Substitute.For<ITaskAttachmentRepository>();
    private readonly IFileStorageService _fileStorageService = Substitute.For<IFileStorageService>();
    private readonly IFileTransactionTracker _fileTracker = Substitute.For<IFileTransactionTracker>();
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    static TaskAttachmentCommandHandlerTests()
    {
        new TaskMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    public TaskAttachmentCommandHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 11, 16, 16, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task UploadAttachmentShouldStoreFileAndRegisterForRollbackAndReturnUrl()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _attachmentRepository.GetTaskAttachmentStatsAsync(taskId, Arg.Any<CancellationToken>())
            .Returns(TaskAttachmentStats.Empty);

        _fileStorageService.SaveFileAsync(
                Arg.Any<Stream>(),
                "report.pdf",
                FileStorageConstants.TaskAttachmentsFolder,
                Arg.Any<CancellationToken>())
            .Returns("stored-123.pdf");

        _fileUrlProvider.GetFileUrl("stored-123.pdf", Arg.Any<string>())
            .Returns("http://localhost:5065/api/v1/projects/p/tasks/t/attachments/a?v=stored-123");

        _attachmentRepository.CreateAsync(Arg.Any<TaskAttachment>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TaskAttachment>());

        var handler = new UploadTaskAttachmentCommandHandler(
            _taskRepository,
            _attachmentRepository,
            _fileStorageService,
            _fileTracker,
            _fileUrlProvider,
            _timeProvider,
            Substitute.For<ILogger<UploadTaskAttachmentCommandHandler>>());

        using var stream = new MemoryStream([1, 2, 3, 4]);
        var dto = await handler.Handle(
            new UploadTaskAttachmentCommand(projectId, taskId, stream, @"C:\fakepath\report.pdf", "application/pdf", 4)
            {
                CurrentUserId = userId,
            },
            CancellationToken.None);

        dto.OriginalFileName.ShouldBe("report.pdf");
        dto.UploadedBy.ShouldBe(userId);
        dto.Url.ShouldBe("http://localhost:5065/api/v1/projects/p/tasks/t/attachments/a?v=stored-123");
        await _taskRepository.Received(1).LockForUpdateAsync(taskId, Arg.Any<CancellationToken>());
        _fileTracker.Received(1).RegisterCreatedFile("stored-123.pdf", FileStorageConstants.TaskAttachmentsFolder);
    }

    [Fact]
    public async Task UploadAttachmentWhenPerTaskCountOrTotalSizeQuotaExceededShouldThrowWithoutSavingFile()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var handler = new UploadTaskAttachmentCommandHandler(
            _taskRepository,
            _attachmentRepository,
            _fileStorageService,
            _fileTracker,
            _fileUrlProvider,
            _timeProvider,
            Substitute.For<ILogger<UploadTaskAttachmentCommandHandler>>());

        using var stream = new MemoryStream([1, 2, 3, 4]);

        _attachmentRepository.GetTaskAttachmentStatsAsync(taskId, Arg.Any<CancellationToken>())
            .Returns(new TaskAttachmentStats(TaskValidationConstants.MaxAttachmentsPerTask, 100L));

        await Should.ThrowAsync<TaskAttachmentLimitExceededException>(() => handler.Handle(
            new UploadTaskAttachmentCommand(projectId, taskId, stream, "report.pdf", "application/pdf", 4)
            {
                CurrentUserId = userId,
            },
            CancellationToken.None).AsTask());

        _attachmentRepository.GetTaskAttachmentStatsAsync(taskId, Arg.Any<CancellationToken>())
            .Returns(new TaskAttachmentStats(5, TaskValidationConstants.MaxTotalAttachmentSizePerTask - 2));

        await Should.ThrowAsync<StorageQuotaExceededException>(() => handler.Handle(
            new UploadTaskAttachmentCommand(projectId, taskId, stream, "report.pdf", "application/pdf", 4)
            {
                CurrentUserId = userId,
            },
            CancellationToken.None).AsTask());

        await _fileStorageService.DidNotReceiveWithAnyArgs().SaveFileAsync(default!, default!, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAttachmentShouldRegisterStoredFileForDeferredDeletion()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        _attachmentRepository.GetByTaskAndIdAsync(taskId, attachmentId, Arg.Any<CancellationToken>())
            .Returns(new TaskAttachment
            {
                Id = attachmentId,
                TaskId = taskId,
                StoredFileName = "stored-123.pdf",
            });

        var handler = new DeleteTaskAttachmentCommandHandler(
            _attachmentRepository,
            _fileTracker,
            Substitute.For<ILogger<DeleteTaskAttachmentCommandHandler>>());

        await handler.Handle(new DeleteTaskAttachmentCommand(projectId, taskId, attachmentId), CancellationToken.None);

        _fileTracker.Received(1).RegisterFileForDeletion("stored-123.pdf", FileStorageConstants.TaskAttachmentsFolder);
        await _attachmentRepository.Received(1).DeleteAsync(attachmentId, Arg.Any<CancellationToken>());
    }
}
