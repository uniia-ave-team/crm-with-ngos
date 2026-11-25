using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Features.Users.Validators;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
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
    [InlineData(nameof(UserDto.Id))]
    [InlineData("id")]
    [InlineData(nameof(UserDto.FirstName))]
    [InlineData("firstname")]
    [InlineData(nameof(UserDto.LastName))]
    [InlineData("lastname")]
    [InlineData(nameof(UserDto.Patronymic))]
    [InlineData("patronymic")]
    [InlineData(nameof(UserDto.Email))]
    [InlineData("email")]
    [InlineData(nameof(UserDto.PhoneNumber))]
    [InlineData("phonenumber")]
    [InlineData(nameof(UserDto.Country))]
    [InlineData("country")]
    [InlineData(nameof(UserDto.EmergencyContact))]
    [InlineData("emergencycontact")]
    [InlineData(nameof(UserDto.Pronouns))]
    [InlineData("pronouns")]
    [InlineData(nameof(UserDto.PronounCategory))]
    [InlineData("pronouncategory")]
    [InlineData(nameof(UserDto.IsActive))]
    [InlineData("isactive")]
    [InlineData(UserConstants.FullName)]
    [InlineData("fullname")]
    [InlineData(nameof(User.CreatedAt))]
    [InlineData("createdat")]
    [InlineData($"  {nameof(UserDto.FirstName)}  ")]
    [InlineData("city")]
    [InlineData("birth_date")]
    [InlineData("t_shirt_size")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.city")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.birth_date")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}['birth_date']")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}[\"t_shirt_size\"]")]
    [InlineData("CUSTOMFIELDS.age")]
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
    [InlineData("PasswordHash")]
    [InlineData("SecurityStamp")]
    [InlineData("ConcurrencyStamp")]
    [InlineData("NormalizedEmail")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.{nameof(UserDto.FirstName)}")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.PasswordHash")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.bad key; DROP TABLE Users;")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.nested.path")]
    [InlineData("invalid column")]
    [InlineData("key; DROP TABLE Users;")]
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
