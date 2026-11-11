using Crm.Application.Dtos.User;
using Crm.Domain.Consts;
using Crm.Domain.Consts.Entities.GeneralConstants;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Mappings;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.Users;

public class AuthUserRepositoryOrderingTests
{
    static AuthUserRepositoryOrderingTests()
    {
        new AuthUserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Theory]
    [InlineData(UserConstants.FullName)]
    [InlineData("LastName")]
    [InlineData("fullname")]
    [InlineData("lastname")]
    public void ApplyOrderingWhenSortingByFullNameAscendingShouldOrderByLastNameFirstNamePatronymic(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Ascending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"LastName\"");
        sql.ShouldContain("\"FirstName\"");
        sql.ShouldContain("\"Patronymic\"");
        sql.ShouldNotContain("DESC");
    }

    [Theory]
    [InlineData(UserConstants.FullName)]
    [InlineData("LastName")]
    [InlineData("fullname")]
    [InlineData("lastname")]
    public void ApplyOrderingWhenSortingByFullNameDescendingShouldOrderByLastNameDescFirstNameDescPatronymicDesc(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Descending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"LastName\" DESC");
        sql.ShouldContain("\"FirstName\" DESC");
        sql.ShouldContain("\"Patronymic\" DESC");
    }

    [Theory]
    [InlineData("FirstName")]
    [InlineData("firstname")]
    public void ApplyOrderingWhenSortingByFirstNameAscendingShouldOrderByFirstNameLastNamePatronymic(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Ascending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"FirstName\"");
        sql.ShouldContain("\"LastName\"");
        sql.ShouldContain("\"Patronymic\"");
        sql.ShouldNotContain("DESC");
    }

    [Theory]
    [InlineData("FirstName")]
    [InlineData("firstname")]
    public void ApplyOrderingWhenSortingByFirstNameDescendingShouldOrderByFirstNameDescLastNameDescPatronymicDesc(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Descending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"FirstName\" DESC");
        sql.ShouldContain("\"LastName\" DESC");
        sql.ShouldContain("\"Patronymic\" DESC");
    }

    [Fact]
    public void ApplyOrderingWhenSortingByEmailShouldDelegateToMapsterSortResolver()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, "Email", SortOrderConstants.Descending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"Email\" DESC");
    }

    [Theory]
    [InlineData("CreatedAt")]
    [InlineData("createdat")]
    public void ApplyOrderingWhenSortingByCreatedAtDescendingShouldOrderByCreatedAtDescending(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Descending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"CreatedAt\" DESC");
    }

    [Theory]
    [InlineData("CreatedAt")]
    [InlineData("createdat")]
    public void ApplyOrderingWhenSortingByCreatedAtAscendingShouldOrderByCreatedAtAscending(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Ascending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"CreatedAt\"");
        sql.ShouldNotContain("DESC");
    }

    [Theory]
    [InlineData("IsActive")]
    [InlineData("isactive")]
    public void ApplyOrderingWhenSortingByIsActiveAscendingShouldOrderByIsActiveAscending(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Ascending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"IsActive\"");
        sql.ShouldNotContain("DESC");
    }

    [Theory]
    [InlineData("IsActive")]
    [InlineData("isactive")]
    public void ApplyOrderingWhenSortingByIsActiveDescendingShouldOrderByIsActiveDescending(string sortField)
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TestableAuthUserRepository(context);
        var baseQuery = context.Set<AuthUser>().AsQueryable();

        // Act
        var orderedQuery = repository.ApplyOrderingPublic<UserDto>(baseQuery, sortField, SortOrderConstants.Descending);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldContain("\"IsActive\" DESC");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestableAuthUserRepository(ApplicationDbContext context)
        : AuthUserRepository(context)
    {
        public IQueryable<AuthUser> ApplyOrderingPublic<TResult>(
            IQueryable<AuthUser> query,
            string orderBy,
            string? sortOrder)
            => ApplyOrdering<TResult>(query, orderBy, sortOrder);
    }
}
