using Crm.Application.Dtos.System;
using Crm.Application.Features.System.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.System.Queries;

/// <summary>
/// Unit tests for <see cref="GetSystemSetupStatusQueryHandler"/>.
/// </summary>
public class GetSystemSetupStatusQueryHandlerTests
{
    [Fact]
    public async Task HandleShouldReturnSetupFlagsVersionAndActiveNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        var userRepository = Substitute.For<IUserRepository>();
        var ngoRepository = Substitute.For<INgoRepository>();
        var versionProvider = Substitute.For<IAppVersionProvider>();
        var noticeCache = Substitute.For<IMaintenanceNoticeCache>();

        var expectedNotice = new MaintenanceNoticeDto(
            "Database upgrade",
            new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc));

        userRepository.AnyAsync(ct).Returns(true);
        ngoRepository.AnyAsync(ct).Returns(false);
        versionProvider.Version.Returns("1.4.0");
        noticeCache.GetActiveNoticeAsync(ct).Returns(expectedNotice);

        var handler = new GetSystemSetupStatusQueryHandler(
            userRepository,
            ngoRepository,
            versionProvider,
            noticeCache,
            NullLogger<GetSystemSetupStatusQueryHandler>.Instance);

        var result = await handler.Handle(new GetSystemSetupStatusQuery(), ct);

        result.HasAdmin.ShouldBeTrue();
        result.HasNgo.ShouldBeFalse();
        result.IsSetupComplete.ShouldBeFalse();
        result.Version.ShouldBe("1.4.0");
        result.Notice.ShouldBe(expectedNotice);
    }

    [Fact]
    public async Task HandleShouldReturnNullNoticeWhenNoActiveNoticeExists()
    {
        var ct = TestContext.Current.CancellationToken;
        var userRepository = Substitute.For<IUserRepository>();
        var ngoRepository = Substitute.For<INgoRepository>();
        var versionProvider = Substitute.For<IAppVersionProvider>();
        var noticeCache = Substitute.For<IMaintenanceNoticeCache>();

        userRepository.AnyAsync(ct).Returns(true);
        ngoRepository.AnyAsync(ct).Returns(true);
        versionProvider.Version.Returns("dev");
        noticeCache.GetActiveNoticeAsync(ct).Returns((MaintenanceNoticeDto?)null);

        var handler = new GetSystemSetupStatusQueryHandler(
            userRepository,
            ngoRepository,
            versionProvider,
            noticeCache,
            NullLogger<GetSystemSetupStatusQueryHandler>.Instance);

        var result = await handler.Handle(new GetSystemSetupStatusQuery(), ct);

        result.HasAdmin.ShouldBeTrue();
        result.HasNgo.ShouldBeTrue();
        result.IsSetupComplete.ShouldBeTrue();
        result.Version.ShouldBe("dev");
        result.Notice.ShouldBeNull();
    }
}
