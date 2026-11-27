using System.Globalization;
using System.Security.Claims;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="JwtBearerEventsHandler"/> verifying access-token claim validation
/// and rejection of users or stale tokens present in <see cref="IRevokedUsersCache"/>.
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
        await cache.DidNotReceiveWithAnyArgs()
            .IsTokenRevokedAsync(default, default, TestContext.Current.CancellationToken);
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
        await cache.DidNotReceiveWithAnyArgs()
            .IsTokenRevokedAsync(default, default, TestContext.Current.CancellationToken);
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
        await cache.DidNotReceiveWithAnyArgs()
            .IsTokenRevokedAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TokenValidatedShouldPassHighPrecisionIssuedAtTicksAndFailWhenRevoked()
    {
        var userId = Guid.CreateVersion7();
        var issuedAt = new DateTimeOffset(2026, 11, 26, 1, 0, 0, 123, TimeSpan.Zero);
        var cache = Substitute.For<IRevokedUsersCache>();
        cache.IsTokenRevokedAsync(userId, issuedAt, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new JwtBearerEventsHandler(cache, NullLogger<JwtBearerEventsHandler>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Sub, userId.ToString()),
            new Claim(CustomClaimTypes.TokenType, CustomClaimTypes.AccessTokenValue),
            new Claim(CustomClaimTypes.IssuedAtTicks, issuedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)),
        ]));
        var context = CreateContext(principal);

        await handler.TokenValidated(context);

        context.Result.ShouldNotBeNull();
        context.Result.Failure.ShouldNotBeNull();
        context.Result.Failure.Message.ShouldContain("revoked");
    }

    [Fact]
    public async Task TokenValidatedShouldFallbackToIatClaimAndSucceedWhenNotRevoked()
    {
        var userId = Guid.CreateVersion7();
        var issuedAtSeconds = new DateTimeOffset(2026, 11, 26, 1, 0, 0, TimeSpan.Zero);
        var cache = Substitute.For<IRevokedUsersCache>();
        cache.IsTokenRevokedAsync(userId, issuedAtSeconds, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new JwtBearerEventsHandler(cache, NullLogger<JwtBearerEventsHandler>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Sub, userId.ToString()),
            new Claim(CustomClaimTypes.TokenType, CustomClaimTypes.AccessTokenValue),
            new Claim(JwtRegisteredClaimNames.Iat, issuedAtSeconds.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
        ]));
        var context = CreateContext(principal);

        await handler.TokenValidated(context);

        context.Result.ShouldBeNull();
        await cache.Received(1).IsTokenRevokedAsync(userId, issuedAtSeconds, Arg.Any<CancellationToken>());
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
