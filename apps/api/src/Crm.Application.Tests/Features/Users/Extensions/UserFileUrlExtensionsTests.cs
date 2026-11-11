using Crm.Application.Common.Consts;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Extensions;

public class UserFileUrlExtensionsTests
{
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveUserAvatarUrlWhenKeyIsNullOrWhitespaceShouldReturnNullWithoutCallingProvider(string? avatarKey)
    {
        // Act
        var result = _fileUrlProvider.ResolveUserAvatarUrl(avatarKey, Guid.NewGuid(), isSelf: false);

        // Assert
        result.ShouldBeNull();
        _fileUrlProvider.DidNotReceiveWithAnyArgs().GetFileUrl(default, default);
    }

    [Fact]
    public void ResolveUserAvatarUrlWhenSelfShouldUseSelfAvatarRoute()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string avatarKey = "avatar-123.jpg";
        _fileUrlProvider.GetFileUrl(avatarKey, ApiRouteLogoConstants.UserSelfAvatar)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = _fileUrlProvider.ResolveUserAvatarUrl(avatarKey, userId, isSelf: true);

        // Assert
        result.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        _fileUrlProvider.Received(1).GetFileUrl(avatarKey, ApiRouteLogoConstants.UserSelfAvatar);
    }

    [Fact]
    public void ResolveUserAvatarUrlWhenNotSelfShouldUseUserAvatarRoute()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string avatarKey = "avatar-456.jpg";
        var expectedRoute = ApiRouteLogoConstants.UserAvatar(userId);
        _fileUrlProvider.GetFileUrl(avatarKey, expectedRoute)
            .Returns($"https://api.crm.org/{expectedRoute}");

        // Act
        var result = _fileUrlProvider.ResolveUserAvatarUrl(avatarKey, userId, isSelf: false);

        // Assert
        result.ShouldBe($"https://api.crm.org/{expectedRoute}");
        _fileUrlProvider.Received(1).GetFileUrl(avatarKey, expectedRoute);
    }
}
