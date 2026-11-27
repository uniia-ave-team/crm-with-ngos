using System.Security.Authentication;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Mappings;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Features.Users.Validators;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users;

/// <summary>
/// Unit tests covering invitation and password-reset handlers backed by <see cref="IUserActionTokenService"/>,
/// paginated filtering/sorting, avatar URL resolution, unified revocation, and validator rules.
/// </summary>
public class UserActionTokenHandlersTests
{
    private readonly IUserActionTokenService _actionTokenService = Substitute.For<IUserActionTokenService>();
    private readonly IAuthUserRepository _authUserRepository = Substitute.For<IAuthUserRepository>();
    private readonly IAuthRoleRepository _authRoleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly INgoRepository _ngoRepository = Substitute.For<INgoRepository>();
    private readonly IUserCustomFieldRepository _userCustomFieldRepository = Substitute.For<IUserCustomFieldRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();

    static UserActionTokenHandlersTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task InviteUserShouldIssueOpaqueInvitationTokenWithDistinctRolesAndCreatedBy()
    {
        var ngoId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        var adminId = Guid.CreateVersion7();
        var expiresAt = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        const string shortToken = "AbCdEfGhIjKlMnOpQrStUvWx";

        _ngoRepository.GetIdAsync(Arg.Any<CancellationToken>()).Returns(ngoId);

        InvitationTokenMetadata? capturedMetadata = null;
        _actionTokenService
            .IssueAsync(
                ActionTokenType.Invitation,
                "newbie@ngo.org",
                Arg.Do<InvitationTokenMetadata>(m => capturedMetadata = m),
                null,
                adminId,
                Arg.Any<CancellationToken>())
            .Returns(new TokenResultDto(shortToken, expiresAt));

        var handler = new InviteUserCommandHandler(
            _authUserRepository,
            _ngoRepository,
            _authRoleRepository,
            _actionTokenService,
            Substitute.For<ILogger<InviteUserCommandHandler>>());

        var command = new InviteUserCommand("  newbie@ngo.org  ", [roleId, roleId]) { CurrentUserId = adminId };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Token.ShouldBe(shortToken);
        result.ExpiresAt.ShouldBe(expiresAt);
        capturedMetadata.ShouldNotBeNull();
        capturedMetadata.NgoId.ShouldBe(ngoId);
        capturedMetadata.RoleIds.ShouldBe([roleId]);
    }

    [Fact]
    public async Task ValidateInvitationShouldReturnDetailsForActiveTokenAndRejectInvalidToken()
    {
        var ngoId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        const string rawToken = "ShortValidInviteToken123";

        _actionTokenService
            .ResolveActiveAsync<InvitationTokenMetadata>(rawToken, ActionTokenType.Invitation, Arg.Any<CancellationToken>())
            .Returns(new ResolvedActionToken<InvitationTokenMetadata>(
                CreateRecord(Guid.CreateVersion7(), ActionTokenType.Invitation, "member@ngo.org"),
                new InvitationTokenMetadata(ngoId, [roleId])));

        var handler = new ValidateInvitationTokenQueryHandler(
            _actionTokenService,
            _authUserRepository,
            Substitute.For<ILogger<ValidateInvitationTokenQueryHandler>>());

        var details = await handler.Handle(new ValidateInvitationTokenQuery(rawToken), CancellationToken.None);

        details.Email.ShouldBe("member@ngo.org");
        details.NgoId.ShouldBe(ngoId);
        details.RoleIds.ShouldBe([roleId]);

        await Should.ThrowAsync<DomainValidationException>(async () =>
            await handler.Handle(new ValidateInvitationTokenQuery("unknown-token"), CancellationToken.None));
    }

    [Fact]
    public async Task CompleteRegistrationShouldConsumeTokenAtomicallyAndRevokeSiblingInvitations()
    {
        var tokenId = Guid.CreateVersion7();
        var ngoId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        var createdUserId = Guid.CreateVersion7();
        const string rawToken = "ShortAcceptInviteToken12";

        _actionTokenService
            .ResolveActiveAsync<InvitationTokenMetadata>(rawToken, ActionTokenType.Invitation, Arg.Any<CancellationToken>())
            .Returns(new ResolvedActionToken<InvitationTokenMetadata>(
                CreateRecord(tokenId, ActionTokenType.Invitation, "member@ngo.org"),
                new InvitationTokenMetadata(ngoId, [roleId])));
        _ngoRepository.GetIdAsync(Arg.Any<CancellationToken>()).Returns(ngoId);
        _actionTokenService.TryConsumeAsync(tokenId, ActionTokenType.Invitation, Arg.Any<CancellationToken>()).Returns(true);
        _authRoleRepository.GetRoleNamesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(["Member"]);
        _userCustomFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _identityService.CreateUserAsync("member@ngo.org", "StrongPass1!", Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(new UserBasicDto(createdUserId, "member@ngo.org"));
        _tokenService.GenerateAccessToken(Arg.Any<UserBasicDto>(), Arg.Any<IList<Guid>>())
            .Returns(new TokenResult("access-jwt", DateTimeOffset.UtcNow.AddMinutes(15)));
        _tokenService.GenerateRefreshToken()
            .Returns(new TokenResult("refresh-opaque", DateTimeOffset.UtcNow.AddDays(7)));

        var handler = new CompleteRegistrationCommandHandler(
            _actionTokenService,
            _tokenService,
            _ngoRepository,
            _authUserRepository,
            _authRoleRepository,
            _userCustomFieldRepository,
            _identityService,
            TimeProvider.System,
            Substitute.For<ILogger<CompleteRegistrationCommandHandler>>());

        var result = await handler.Handle(
            new CompleteRegistrationCommand(rawToken, "Olena", "Petrenko", null, "StrongPass1!", "StrongPass1!"),
            CancellationToken.None);

        result.AccessToken.ShouldBe("access-jwt");
        result.RefreshToken.ShouldBe("refresh-opaque");
        await _actionTokenService.Received(1).TryConsumeAsync(tokenId, ActionTokenType.Invitation, Arg.Any<CancellationToken>());
        await _actionTokenService.Received(1).RevokeAllByTargetEmailAsync("member@ngo.org", ActionTokenType.Invitation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteRegistrationWhenConcurrentReplayLosesConsumeRaceShouldThrowValidationException()
    {
        var tokenId = Guid.CreateVersion7();
        var ngoId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        const string rawToken = "ShortReplayInviteToken12";

        _actionTokenService
            .ResolveActiveAsync<InvitationTokenMetadata>(rawToken, ActionTokenType.Invitation, Arg.Any<CancellationToken>())
            .Returns(new ResolvedActionToken<InvitationTokenMetadata>(
                CreateRecord(tokenId, ActionTokenType.Invitation, "member@ngo.org"),
                new InvitationTokenMetadata(ngoId, [roleId])));
        _ngoRepository.GetIdAsync(Arg.Any<CancellationToken>()).Returns(ngoId);
        _actionTokenService.TryConsumeAsync(tokenId, ActionTokenType.Invitation, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CompleteRegistrationCommandHandler(
            _actionTokenService,
            _tokenService,
            _ngoRepository,
            _authUserRepository,
            _authRoleRepository,
            _userCustomFieldRepository,
            _identityService,
            TimeProvider.System,
            Substitute.For<ILogger<CompleteRegistrationCommandHandler>>());

        await Should.ThrowAsync<DomainValidationException>(async () =>
            await handler.Handle(
                new CompleteRegistrationCommand(rawToken, "Olena", "Petrenko", null, "StrongPass1!", "StrongPass1!"),
                CancellationToken.None));

        await _identityService.DidNotReceiveWithAnyArgs()
            .CreateUserAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GenerateRestorePasswordTokenShouldIssueOpaqueTokenInSingleIdentityLookup()
    {
        var targetUserId = Guid.CreateVersion7();
        var adminId = Guid.CreateVersion7();
        var expiresAt = new DateTime(2026, 12, 2, 12, 0, 0, DateTimeKind.Utc);
        const string shortToken = "ShortResetToken123456789";
        const string identityResetToken = "CfDJ8IdentityRawResetTokenPayload";

        _identityService.GetResetTokenAsync(targetUserId, Arg.Any<CancellationToken>())
            .Returns(new IdentityPasswordResetTokenResult("user@ngo.org", identityResetToken));

        PasswordResetTokenMetadata? capturedMetadata = null;
        _actionTokenService
            .IssueAsync(
                ActionTokenType.PasswordReset,
                "user@ngo.org",
                Arg.Do<PasswordResetTokenMetadata>(m => capturedMetadata = m),
                targetUserId,
                adminId,
                Arg.Any<CancellationToken>())
            .Returns(new TokenResultDto(shortToken, expiresAt));

        var handler = new GenerateRestorePasswordTokenCommandHandler(
            _identityService,
            _actionTokenService,
            Substitute.For<ILogger<GenerateRestorePasswordTokenCommandHandler>>());

        var result = await handler.Handle(
            new GenerateRestorePasswordTokenCommand(targetUserId) { CurrentUserId = adminId },
            CancellationToken.None);

        result.Token.ShouldBe(shortToken);
        result.ExpiresAt.ShouldBe(expiresAt);
        capturedMetadata.ShouldNotBeNull();
        capturedMetadata.IdentityResetToken.ShouldBe(identityResetToken);
    }

    [Fact]
    public async Task ValidatePasswordResetTokenShouldReturnTargetUserDetailsAndAvatarUrl()
    {
        var userId = Guid.CreateVersion7();
        var expiresAt = new DateTime(2026, 12, 3, 18, 0, 0, DateTimeKind.Utc);
        const string rawToken = "ShortValidResetToken1234";

        _actionTokenService
            .ResolveActiveAsync<PasswordResetTokenMetadata>(rawToken, ActionTokenType.PasswordReset, Arg.Any<CancellationToken>())
            .Returns(new ResolvedActionToken<PasswordResetTokenMetadata>(
                CreateRecord(
                    Guid.CreateVersion7(),
                    ActionTokenType.PasswordReset,
                    "olena@ngo.org",
                    targetUserId: userId,
                    firstName: "Olena",
                    lastName: "Petrenko",
                    avatar: "avatars/olena.png",
                    expiresAt: expiresAt),
                new PasswordResetTokenMetadata("identity-reset-payload")));
        _fileUrlProvider
            .GetFileUrl("avatars/olena.png", Arg.Any<string>())
            .Returns($"https://crm.ngo.org/api/v1/users/{userId}/avatar");

        var handler = new ValidatePasswordResetTokenQueryHandler(
            _actionTokenService,
            _fileUrlProvider,
            Substitute.For<ILogger<ValidatePasswordResetTokenQueryHandler>>());

        var details = await handler.Handle(new ValidatePasswordResetTokenQuery(rawToken), CancellationToken.None);

        details.Email.ShouldBe("olena@ngo.org");
        details.FirstName.ShouldBe("Olena");
        details.LastName.ShouldBe("Petrenko");
        details.AvatarUrl.ShouldBe($"https://crm.ngo.org/api/v1/users/{userId}/avatar");
        details.ExpiresAt.ShouldBe(expiresAt);

        await Should.ThrowAsync<DomainValidationException>(async () =>
            await handler.Handle(new ValidatePasswordResetTokenQuery("revoked-or-expired"), CancellationToken.None));
    }

    [Fact]
    public async Task ResetPasswordShouldConsumeTokenAtomicallyAndRejectReusedOrInvalidTokens()
    {
        var tokenId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        const string rawToken = "ShortValidResetToken5678";
        const string identityPayload = "identity-reset-payload";

        _actionTokenService
            .ResolveActiveAsync<PasswordResetTokenMetadata>(rawToken, ActionTokenType.PasswordReset, Arg.Any<CancellationToken>())
            .Returns(new ResolvedActionToken<PasswordResetTokenMetadata>(
                CreateRecord(tokenId, ActionTokenType.PasswordReset, "user@ngo.org", targetUserId: userId),
                new PasswordResetTokenMetadata(identityPayload)));
        _actionTokenService.TryConsumeAsync(tokenId, ActionTokenType.PasswordReset, Arg.Any<CancellationToken>())
            .Returns(true, false);

        var handler = new ResetPasswordCommandHandler(
            _actionTokenService,
            _identityService,
            Substitute.For<ILogger<ResetPasswordCommandHandler>>());

        await handler.Handle(new ResetPasswordCommand(rawToken, "NewStrongPass1!"), CancellationToken.None);

        await _identityService.Received(1)
            .ResetPasswordAsync(userId, identityPayload, "NewStrongPass1!", Arg.Any<CancellationToken>());

        // Second submission loses TryConsume race and fails with InvalidCredentialException
        await Should.ThrowAsync<InvalidCredentialException>(async () =>
            await handler.Handle(new ResetPasswordCommand(rawToken, "AnotherPass1!"), CancellationToken.None));

        // Unknown/revoked token fails with InvalidCredentialException
        await Should.ThrowAsync<InvalidCredentialException>(async () =>
            await handler.Handle(new ResetPasswordCommand("missing-token", "AnotherPass1!"), CancellationToken.None));
    }

    [Fact]
    public async Task PagedListAndUnifiedRevokeHandlersShouldReturnPagedResultsWithAvatarsAndDelegateRevocation()
    {
        var inviteId = Guid.CreateVersion7();
        var resetId = Guid.CreateVersion7();
        var ngoId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var adminId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        var now = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

        _actionTokenService
            .GetPagedActiveWithMetadataAsync<InvitationTokenMetadata>(
                ActionTokenType.Invitation,
                Arg.Is<UserActionTokenFilter>(f => f.SearchTerm == "invitee" && f.PageNumber == 1 && f.PageSize == 20 && f.CreatedBy == adminId),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ResolvedActionToken<InvitationTokenMetadata>>(
                [
                    new ResolvedActionToken<InvitationTokenMetadata>(
                        CreateRecord(inviteId, ActionTokenType.Invitation, "invitee@ngo.org", createdAt: now, expiresAt: now.AddHours(48), createdBy: adminId),
                        new InvitationTokenMetadata(ngoId, [roleId])),
                ],
                totalCount: 1,
                pageNumber: 1,
                pageSize: 20));

        _actionTokenService
            .GetPagedActiveAsync<ActivePasswordResetTokenDto>(
                ActionTokenType.PasswordReset,
                Arg.Is<UserActionTokenFilter>(f => f.TargetUserId == userId && f.SearchTerm == "Petro" && f.PageNumber == 1 && f.PageSize == 10),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ActivePasswordResetTokenDto>(
                [
                    new ActivePasswordResetTokenDto(
                        resetId,
                        userId,
                        "member@ngo.org",
                        "Petro",
                        "Koval",
                        "avatars/petro.png",
                        now,
                        now.AddHours(72),
                        adminId),
                ],
                totalCount: 1,
                pageNumber: 1,
                pageSize: 10));

        _fileUrlProvider
            .GetFileUrl("avatars/petro.png", Arg.Any<string>())
            .Returns($"https://crm.ngo.org/api/v1/users/{userId}/avatar");

        var listInvitesHandler = new GetActiveInvitationsQueryHandler(
            _actionTokenService,
            Substitute.For<ILogger<GetActiveInvitationsQueryHandler>>());
        var invitesPage = await listInvitesHandler.Handle(
            new GetActiveInvitationsQuery(SearchTerm: "invitee", PageNumber: 1, PageSize: 20, CreatedBy: adminId),
            CancellationToken.None);

        invitesPage.TotalCount.ShouldBe(1);
        invitesPage.PageSize.ShouldBe(20);
        var invite = invitesPage.Items.Single();
        invite.Id.ShouldBe(inviteId);
        invite.Email.ShouldBe("invitee@ngo.org");
        invite.RoleIds.ShouldBe([roleId]);
        invite.CreatedBy.ShouldBe(adminId);

        var listResetsHandler = new GetActivePasswordResetTokensQueryHandler(
            _actionTokenService,
            _fileUrlProvider,
            Substitute.For<ILogger<GetActivePasswordResetTokensQueryHandler>>());
        var resetsPage = await listResetsHandler.Handle(
            new GetActivePasswordResetTokensQuery(SearchTerm: "Petro", UserId: userId),
            CancellationToken.None);

        resetsPage.TotalCount.ShouldBe(1);
        var reset = resetsPage.Items.Single();
        reset.Id.ShouldBe(resetId);
        reset.UserId.ShouldBe(userId);
        reset.Email.ShouldBe("member@ngo.org");
        reset.FirstName.ShouldBe("Petro");
        reset.LastName.ShouldBe("Koval");
        reset.AvatarUrl.ShouldBe($"https://crm.ngo.org/api/v1/users/{userId}/avatar");

        var revokeHandler = new RevokeActionTokenCommandHandler(
            _actionTokenService,
            Substitute.For<ILogger<RevokeActionTokenCommandHandler>>());

        await revokeHandler.Handle(new RevokeActionTokenCommand(inviteId, ActionTokenType.Invitation), CancellationToken.None);
        await revokeHandler.Handle(new RevokeActionTokenCommand(resetId, ActionTokenType.PasswordReset), CancellationToken.None);

        await _actionTokenService.Received(1).RevokeByIdAsync(inviteId, ActionTokenType.Invitation, Arg.Any<CancellationToken>());
        await _actionTokenService.Received(1).RevokeByIdAsync(resetId, ActionTokenType.PasswordReset, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ActionTokenValidatorsShouldEnforcePaginationSortingAndFilterRules()
    {
        new ValidatePasswordResetTokenQueryValidator().Validate(new ValidatePasswordResetTokenQuery(string.Empty)).IsValid.ShouldBeFalse();
        new ValidatePasswordResetTokenQueryValidator().Validate(new ValidatePasswordResetTokenQuery("valid-token")).IsValid.ShouldBeTrue();

        new RevokeActionTokenCommandValidator().Validate(new RevokeActionTokenCommand(Guid.Empty, ActionTokenType.Invitation)).IsValid.ShouldBeFalse();
        new RevokeActionTokenCommandValidator().Validate(new RevokeActionTokenCommand(Guid.CreateVersion7(), (ActionTokenType)99)).IsValid.ShouldBeFalse();
        new RevokeActionTokenCommandValidator().Validate(new RevokeActionTokenCommand(Guid.CreateVersion7(), ActionTokenType.Invitation)).IsValid.ShouldBeTrue();

        var inviteListValidator = new GetActiveInvitationsQueryValidator();
        inviteListValidator.Validate(new GetActiveInvitationsQuery(PageNumber: 0)).IsValid.ShouldBeFalse();
        inviteListValidator.Validate(new GetActiveInvitationsQuery(OrderBy: "InvalidField")).IsValid.ShouldBeFalse();
        inviteListValidator.Validate(new GetActiveInvitationsQuery(CreatedBy: Guid.Empty)).IsValid.ShouldBeFalse();
        inviteListValidator.Validate(new GetActiveInvitationsQuery(SearchTerm: "ngo", OrderBy: "ExpiresAt", SortOrder: "asc", PageNumber: 1, PageSize: 25)).IsValid.ShouldBeTrue();

        var resetListValidator = new GetActivePasswordResetTokensQueryValidator();
        resetListValidator.Validate(new GetActivePasswordResetTokensQuery(PageSize: 0)).IsValid.ShouldBeFalse();
        resetListValidator.Validate(new GetActivePasswordResetTokensQuery(OrderBy: "Unknown")).IsValid.ShouldBeFalse();
        resetListValidator.Validate(new GetActivePasswordResetTokensQuery(UserId: Guid.Empty)).IsValid.ShouldBeFalse();
        resetListValidator.Validate(new GetActivePasswordResetTokensQuery(CreatedBy: Guid.Empty)).IsValid.ShouldBeFalse();
        resetListValidator.Validate(new GetActivePasswordResetTokensQuery(SearchTerm: "Petro", OrderBy: "FullName", SortOrder: "asc", UserId: Guid.CreateVersion7())).IsValid.ShouldBeTrue();
    }

    private static UserActionTokenRecord CreateRecord(
        Guid id,
        ActionTokenType type,
        string email,
        Guid? targetUserId = null,
        string? firstName = null,
        string? lastName = null,
        string? avatar = null,
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        Guid? createdBy = null)
        => new(
            id,
            type,
            email,
            targetUserId,
            firstName,
            lastName,
            avatar,
            EncryptedMetadata: "v1.encrypted",
            createdAt ?? DateTime.UtcNow,
            expiresAt ?? DateTime.UtcNow.AddHours(24),
            createdBy);
}
