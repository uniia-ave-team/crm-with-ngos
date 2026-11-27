using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Features.Users.Validators;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Validators;

public class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _validator = new();

    [Fact]
    public void ValidateWhenDefaultQueryShouldBeValid()
    {
        var query = new GetUsersQuery();

        var result = _validator.Validate(query);

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
    [InlineData("019a0000-0000-7000-8000-000000000001")]
    [InlineData("019A0000-0000-7000-8000-000000000001")]
    public void ValidateWhenValidOrderByProvidedShouldPass(string orderBy)
    {
        var query = new GetUsersQuery(OrderBy: orderBy);

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("city")]
    [InlineData("f_019a0000000000000000000000000001")]
    [InlineData("PasswordHash")]
    [InlineData("SecurityStamp")]
    [InlineData("ConcurrencyStamp")]
    [InlineData("NormalizedEmail")]
    [InlineData("customFields.city")]
    [InlineData("customFields['birth_date']")]
    [InlineData("customFields.nested.path")]
    [InlineData("invalid column")]
    [InlineData("key; DROP TABLE Users;")]
    public void ValidateWhenInvalidOrderByProvidedShouldFail(string invalidOrderBy)
    {
        var query = new GetUsersQuery(OrderBy: invalidOrderBy);

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.OrderBy));
    }

    [Fact]
    public void ValidateWhenPageNumberLessThanOneShouldFail()
    {
        var query = new GetUsersQuery(PageNumber: 0);

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.PageNumber));
    }

    [Fact]
    public void ValidateWhenPageSizeExceedsMaximumShouldFail()
    {
        var query = new GetUsersQuery(PageSize: PaginationConstants.MaxPageSize + 1);

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.PageSize));
    }

    [Fact]
    public void ValidateWhenInvalidSortOrderShouldFail()
    {
        var query = new GetUsersQuery(SortOrder: "RANDOM");

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.SortOrder));
    }

    [Theory]
    [InlineData("Я")]
    [InlineData("Ян")]
    [InlineData("  Ян  ")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ValidateWhenShortOrEmptySearchTermProvidedShouldPass(string? searchTerm)
    {
        var query = new GetUsersQuery(SearchTerm: searchTerm);

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWhenSearchTermExceedsMaxLengthShouldFail()
    {
        var query = new GetUsersQuery(SearchTerm: new string('a', SearchValidationConstants.MaxSearchTermLength + 1));

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersQuery.SearchTerm));
    }
}
