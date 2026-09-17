using Crm.Domain.Exceptions;
using Shouldly;

namespace Crm.Domain.Tests.Exceptions;

/// <summary>
/// Contains unit tests for domain-specific exceptions to verify correct
/// HTTP status codes and formatted error messages.
/// </summary>
public class DomainExceptionsTests
{
    /// <summary>
    /// Verifies that the <see cref="EntityNotFoundException"/> correctly formats its message
    /// and assigns a 404 status code when an explicit key is provided.
    /// </summary>
    [Fact]
    public void EntityNotFoundExceptionWithKeyShouldFormatMessageProperly()
    {
        // Arrange
        var entityType = "User";
        var key = Guid.Empty;

        // Act
        var exception = new EntityNotFoundException(entityType, key);

        // Assert
        exception.Message.ShouldBe($"Entity 'User' with key '{Guid.Empty}' was not found.");
        exception.StatusCode.ShouldBe(404);
    }

    /// <summary>
    /// Verifies that the <see cref="EntityNotFoundException"/> correctly formats its message
    /// when no specific key is provided.
    /// </summary>
    [Fact]
    public void EntityNotFoundExceptionWithoutKeyShouldFormatMessageProperly()
    {
        // Arrange
        var entityType = "User";

        // Act
        var exception = new EntityNotFoundException(entityType);

        // Assert
        exception.Message.ShouldBe("Entity 'User' was not found.");
        exception.StatusCode.ShouldBe(404);
    }

    /// <summary>
    /// Verifies that the <see cref="EntityAlreadyExistsException"/> correctly formats its message
    /// and assigns a 409 Conflict status code when a key is provided.
    /// </summary>
    [Fact]
    public void EntityAlreadyExistsExceptionWithKeyShouldFormatMessageProperly()
    {
        // Arrange
        var entityType = "Ngo";
        var key = 123;

        // Act
        var exception = new EntityAlreadyExistsException(entityType, key);

        // Assert
        exception.Message.ShouldBe("Entity 'Ngo' with key '123' already exists.");
        exception.StatusCode.ShouldBe(409);
    }

    /// <summary>
    /// Verifies that the <see cref="EntitiesNotFoundException"/> correctly formats its message
    /// and assigns a 404 status code when multiple missing keys are provided.
    /// </summary>
    [Fact]
    public void EntitiesNotFoundExceptionWithMultipleKeysShouldFormatMessageProperly()
    {
        // Arrange
        var entityType = "Role";
        var keys = new object[] { 1, 2, 3 };

        // Act
        var exception = new EntitiesNotFoundException(entityType, keys);

        // Assert
        exception.Message.ShouldBe("Entities of type 'Role' with keys ['1', '2', '3'] were not found.");
        exception.StatusCode.ShouldBe(404);
    }

    /// <summary>
    /// Verifies that the <see cref="EntitiesNotFoundException"/> uses a fallback message format
    /// when an empty collection of keys is provided.
    /// </summary>
    [Fact]
    public void EntitiesNotFoundExceptionWithEmptyKeysShouldFormatMessageProperly()
    {
        // Arrange
        var entityType = "Role";
        var emptyKeys = Array.Empty<object>();

        // Act
        var exception = new EntitiesNotFoundException(entityType, emptyKeys);

        // Assert
        exception.Message.ShouldContain("One or more entities of type 'Role' were not found.");
        exception.StatusCode.ShouldBe(404);
    }
}
