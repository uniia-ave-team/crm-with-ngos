using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Features.Users.Validators;
using Crm.Application.Interfaces;
using Crm.Cli.Services;
using NSubstitute;
using Shouldly;

namespace Crm.Cli.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CompliantPasswordGenerator"/>.
/// </summary>
public class CompliantPasswordGeneratorTests
{
    [Theory]
    [InlineData(8, true, true, true, true, 16)]
    [InlineData(24, true, true, true, true, 24)]
    [InlineData(12, false, false, false, false, 16)]
    [InlineData(4, true, true, true, true, 16)]
    public async Task GenerateShouldAlwaysSatisfyCreateUserCommandValidator(
        int requiredLength,
        bool requireDigit,
        bool requireNonAlphanumeric,
        bool requireUppercase,
        bool requireLowercase,
        int expectedLength)
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new IdentityOptionsDto(
            RequiredLength: requiredLength,
            RequireDigit: requireDigit,
            RequireNonAlphanumeric: requireNonAlphanumeric,
            RequireUppercase: requireUppercase,
            RequireLowercase: requireLowercase,
            RequireUniqueEmail: true);

        var optionService = Substitute.For<IOptionService>();
        optionService.GetIdentityOptions().Returns(options);
        var validator = new CreateUserCommandValidator(optionService);

        string password = CompliantPasswordGenerator.Generate(options);

        password.Length.ShouldBe(expectedLength);

        var command = new CreateUserCommand(
            FirstName: "Olena",
            LastName: "Kovalenko",
            Patronymic: null,
            Email: "admin@example.org",
            Password: password,
            ConfirmPassword: password);

        var validation = await validator.ValidateAsync(command, ct);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
    }
}
