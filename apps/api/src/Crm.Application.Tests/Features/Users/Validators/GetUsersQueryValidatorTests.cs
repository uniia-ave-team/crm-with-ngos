using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Users.Validators;
using Crm.Domain.Consts;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Validators;

public class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _validator = new();

    [Fact]
    public void ValidateWhenDefaultQueryShouldBeValid()
    {
        // Arrange
        var query = new GetUsersQuery();

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("id")]
    [InlineData("FirstName")]
    [InlineData("firstname")]
    [InlineData("LastName")]
    [InlineData("lastname")]
    [InlineData("Patronymic")]
    [InlineData("patronymic")]
    [InlineData("Email")]
    [InlineData("email")]
    [InlineData("PhoneNumber")]
    [InlineData("phonenumber")]
    [InlineData("Country")]
    [InlineData("country")]
    [InlineData("EmergencyContact")]
    [InlineData("emergencycontact")]
    [InlineData("Pronouns")]
    [InlineData("pronouns")]
    [InlineData("PronounCategory")]
    [InlineData("pronouncategory")]
    [InlineData("IsActive")]
    [InlineData("isactive")]
    [InlineData("FullName")]
    [InlineData("fullname")]
    [InlineData("CreatedAt")]
    [InlineData("createdat")]
    [InlineData("  FirstName  ")]
    public void ValidateWhenValidOrderByProvidedShouldPass(string orderBy)
    {
        // Arrange
        var query = new GetUsersQuery(OrderBy: orderBy);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("PasswordHash")]
    [InlineData("SecretKey")]
    [InlineData("DropTableUsers")]
    [InlineData("InvalidColumn")]
    public void ValidateWhenInvalidOrderByProvidedShouldFail(string invalidOrderBy)
    {
        // Arrange
        var query = new GetUsersQuery(OrderBy: invalidOrderBy);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.OrderBy));
    }

    [Fact]
    public void ValidateWhenPageNumberLessThanOneShouldFail()
    {
        // Arrange
        var query = new GetUsersQuery(PageNumber: 0);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.PageNumber));
    }

    [Fact]
    public void ValidateWhenPageSizeExceedsMaximumShouldFail()
    {
        // Arrange
        var query = new GetUsersQuery(PageSize: PaginationConstants.MaxPageSize + 1);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.PageSize));
    }

    [Fact]
    public void ValidateWhenInvalidSortOrderShouldFail()
    {
        // Arrange
        var query = new GetUsersQuery(SortOrder: "RANDOM");

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.SortOrder));
    }
}
