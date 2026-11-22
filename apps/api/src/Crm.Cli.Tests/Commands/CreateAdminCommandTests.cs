using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Enums;
using Crm.Application.Exceptions;
using Crm.Application.Features.Users.Validators;
using Crm.Application.Interfaces;
using Crm.Cli.Abstractions;
using Crm.Cli.Commands;
using Crm.Cli.Parsing;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Crm.Cli.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="CreateAdminCommand"/>.
/// </summary>
public class CreateAdminCommandTests
{
    private static readonly HashSet<string> BooleanFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "help",
        "h",
        "password-stdin",
    };

    [Fact]
    public async Task ExecuteShouldCreateAdminUsingPasswordFromStdinAndNotPrintPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var createdId = Guid.NewGuid();
        var handler = new StubCreateUserHandler(_ => createdId);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var optionService = CreateDefaultOptionService();
        using var console = new FakeCliConsole("StrongPass1!\n");

        var command = CreateCommand(handler, unitOfWork, optionService, console);
        var args = CommandLineParser.Parse(
            ["create-admin", "--email", "admin@example.org", "--first-name", "Olena", "--last-name", "Kovalenko", "--patronymic", "Petrivna", "--password-stdin"],
            BooleanFlags);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(0);
        handler.CallCount.ShouldBe(1);
        handler.LastCommand.ShouldNotBeNull();
        handler.LastCommand.Email.ShouldBe("admin@example.org");
        handler.LastCommand.FirstName.ShouldBe("Olena");
        handler.LastCommand.LastName.ShouldBe("Kovalenko");
        handler.LastCommand.Patronymic.ShouldBe("Petrivna");
        handler.LastCommand.Password.ShouldBe("StrongPass1!");
        handler.LastCommand.ConfirmPassword.ShouldBe("StrongPass1!");
        console.Output.ShouldBeEmpty();

        await unitOfWork.Received(1).BeginTransactionAsync(ct);
        await unitOfWork.Received(1).CommitTransactionAsync(ct);
        await unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteShouldGenerateCompliantPasswordAndPrintItOnceWhenStdinFlagIsNotProvided()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubCreateUserHandler(_ => Guid.NewGuid());

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var optionService = CreateDefaultOptionService();
        using var console = new FakeCliConsole();

        var command = CreateCommand(handler, unitOfWork, optionService, console);
        var args = CommandLineParser.Parse(
            ["create-admin", "--email", "admin@example.org", "--first-name", "Olena", "--last-name", "Kovalenko"],
            BooleanFlags);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(0);
        handler.CallCount.ShouldBe(1);
        handler.LastCommand.ShouldNotBeNull();
        handler.LastCommand.Password.Length.ShouldBeGreaterThanOrEqualTo(16);
        handler.LastCommand.ConfirmPassword.ShouldBe(handler.LastCommand.Password);
        console.Output.Trim().ShouldBe($"Generated password: {handler.LastCommand.Password}");
    }

    [Fact]
    public async Task ExecuteShouldFailWhenUsersAlreadyExistAndNotPrintGeneratedPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubCreateUserHandler(_ => throw new EntityAlreadyExistsException(nameof(User)));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var optionService = CreateDefaultOptionService();
        using var console = new FakeCliConsole();

        var command = CreateCommand(handler, unitOfWork, optionService, console);
        var args = CommandLineParser.Parse(
            ["create-admin", "--email", "admin@example.org", "--first-name", "Olena", "--last-name", "Kovalenko"],
            BooleanFlags);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(1);
        console.Output.ShouldBeEmpty();
        await unitOfWork.Received(1).BeginTransactionAsync(ct);
        await unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).RollbackTransactionAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteShouldFailWhenPasswordViolatesIdentityOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubCreateUserHandler(_ => Guid.NewGuid());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var optionService = CreateDefaultOptionService();
        using var console = new FakeCliConsole("weak\n");

        var command = CreateCommand(handler, unitOfWork, optionService, console);
        var args = CommandLineParser.Parse(
            ["create-admin", "--email", "admin@example.org", "--first-name", "Olena", "--last-name", "Kovalenko", "--password-stdin"],
            BooleanFlags);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(1);
        handler.CallCount.ShouldBe(0);
        await unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteShouldFailWhenRequiredArgumentsAreMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubCreateUserHandler(_ => Guid.NewGuid());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var optionService = CreateDefaultOptionService();
        using var console = new FakeCliConsole();

        var command = CreateCommand(handler, unitOfWork, optionService, console);
        var args = CommandLineParser.Parse(["create-admin", "--email", "admin@example.org"], BooleanFlags);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(1);
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteShouldFailWhenPasswordStdinIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubCreateUserHandler(_ => Guid.NewGuid());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var optionService = CreateDefaultOptionService();
        using var console = new FakeCliConsole(string.Empty);

        var command = CreateCommand(handler, unitOfWork, optionService, console);
        var args = CommandLineParser.Parse(
            ["create-admin", "--email", "admin@example.org", "--first-name", "Olena", "--last-name", "Kovalenko", "--password-stdin"],
            BooleanFlags);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(1);
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteShouldRollbackAndFailWhenIdentityRejectsCreation()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubCreateUserHandler(_ => throw new UserOperationException(UserOperation.Create, "Identity validation failed."));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var optionService = CreateDefaultOptionService();
        using var console = new FakeCliConsole("StrongPass1!\n");

        var command = CreateCommand(handler, unitOfWork, optionService, console);
        var args = CommandLineParser.Parse(
            ["create-admin", "--email", "admin@example.org", "--first-name", "Olena", "--last-name", "Kovalenko", "--password-stdin"],
            BooleanFlags);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(1);
        await unitOfWork.Received(1).RollbackTransactionAsync(CancellationToken.None);
    }

    private static IOptionService CreateDefaultOptionService()
    {
        var optionService = Substitute.For<IOptionService>();
        optionService.GetIdentityOptions().Returns(new IdentityOptionsDto(
            RequiredLength: 8,
            RequireDigit: true,
            RequireNonAlphanumeric: true,
            RequireUppercase: true,
            RequireLowercase: true,
            RequireUniqueEmail: true));
        return optionService;
    }

    private static CreateAdminCommand CreateCommand(
        IRequestHandler<CreateUserCommand, Guid> handler,
        IUnitOfWork unitOfWork,
        IOptionService optionService,
        ICliConsole console)
        => new(
            handler,
            new CreateUserCommandValidator(optionService),
            unitOfWork,
            optionService,
            console,
            NullLogger<CreateAdminCommand>.Instance);

    private sealed class StubCreateUserHandler(Func<CreateUserCommand, Guid> onHandle)
        : IRequestHandler<CreateUserCommand, Guid>
    {
        public int CallCount { get; private set; }

        public CreateUserCommand? LastCommand { get; private set; }

        public ValueTask<Guid> Handle(CreateUserCommand message, CancellationToken cancellationToken)
        {
            CallCount++;
            LastCommand = message;
            return ValueTask.FromResult(onHandle(message));
        }
    }

    private sealed class FakeCliConsole(string input = "") : ICliConsole, IDisposable
    {
        private readonly StringReader _in = new(input);
        private readonly StringWriter _out = new();
        private readonly StringWriter _error = new();

        public TextReader In => _in;

        public TextWriter Out => _out;

        public TextWriter Error => _error;

        public bool IsInputRedirected => true;

        public string Output => _out.ToString();

        public void Dispose()
        {
            _in.Dispose();
            _out.Dispose();
            _error.Dispose();
        }
    }
}
