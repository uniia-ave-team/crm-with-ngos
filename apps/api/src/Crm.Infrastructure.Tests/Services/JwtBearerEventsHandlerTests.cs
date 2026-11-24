using System.Security.Claims;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="JwtBearerEventsHandler"/> verifying access-token claim validation
/// and rejection of users present in <see cref="IRevokedUsersCache"/>.
/// </summary>
public class JwtBearerEventsHandlerTests
{
    [Fact]
    public async Task TokenValidatedShouldFailWhenPrincipalIsNull()
    {
        var cache = Substitute.For<IRevokedUsersCache>();
        var handler = new JwtBearerEventsHandler(cache, NullLogger<JwtBearerEventsHandler>.Instance);
        var context = CreateContext(principal: null);

        await handler.TokenValidated(context);

        context.Result.ShouldNotBeNull();
        context.Result.Failure.ShouldNotBeNull();
        await cache.DidNotReceive().IsUserRevokedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TokenValidatedShouldFailWhenTokenIsNotAccessToken()
    {
        var cache = Substitute.For<IRevokedUsersCache>();
        var handler = new JwtBearerEventsHandler(cache, NullLogger<JwtBearerEventsHandler>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Sub, Guid.CreateVersion7().ToString()),
            new Claim(CustomClaimTypes.TokenType, CustomClaimTypes.InvitationTokenValue),
        ]));
        var context = CreateContext(principal);

        await handler.TokenValidated(context);

        context.Result.ShouldNotBeNull();
        context.Result.Failure.ShouldNotBeNull();
        await cache.DidNotReceive().IsUserRevokedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TokenValidatedShouldFailWhenUserIdClaimIsMissing()
    {
        var cache = Substitute.For<IRevokedUsersCache>();
        var handler = new JwtBearerEventsHandler(cache, NullLogger<JwtBearerEventsHandler>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.TokenType, CustomClaimTypes.AccessTokenValue),
        ]));
        var context = CreateContext(principal);

        await handler.TokenValidated(context);

        context.Result.ShouldNotBeNull();
        context.Result.Failure.ShouldNotBeNull();
        await cache.DidNotReceive().IsUserRevokedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TokenValidatedShouldFailWhenUserIsInRevokedCache()
    {
        var userId = Guid.CreateVersion7();
        var cache = Substitute.For<IRevokedUsersCache>();
        cache.IsUserRevokedAsync(userId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new JwtBearerEventsHandler(cache, NullLogger<JwtBearerEventsHandler>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Sub, userId.ToString()),
            new Claim(CustomClaimTypes.TokenType, CustomClaimTypes.AccessTokenValue),
        ]));
        var context = CreateContext(principal);

        await handler.TokenValidated(context);

        context.Result.ShouldNotBeNull();
        context.Result.Failure.ShouldNotBeNull();
        context.Result.Failure.Message.ShouldContain("revoked");
    }

    [Fact]
    public async Task TokenValidatedShouldSucceedWhenAccessTokenIsValidAndUserIsNotRevoked()
    {
        var userId = Guid.CreateVersion7();
        var cache = Substitute.For<IRevokedUsersCache>();
        cache.IsUserRevokedAsync(userId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new JwtBearerEventsHandler(cache, NullLogger<JwtBearerEventsHandler>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Sub, userId.ToString()),
            new Claim(CustomClaimTypes.TokenType, CustomClaimTypes.AccessTokenValue),
        ]));
        var context = CreateContext(principal);

        await handler.TokenValidated(context);

        context.Result.ShouldBeNull();
    }

    private static TokenValidatedContext CreateContext(ClaimsPrincipal? principal)
    {
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        return new TokenValidatedContext(new DefaultHttpContext(), scheme, new JwtBearerOptions())
        {
            Principal = principal,
        };
    }
}
