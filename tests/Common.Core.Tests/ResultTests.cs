using Common.Core;
using FluentAssertions;

namespace Common.Core.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorType.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        // Arrange
        var errorType = ErrorType.Validation;
        var errorMessage = "Validation failed";

        // Act
        var result = Result.Failure(errorType, errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(errorType);
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void SuccessGeneric_CreatesSuccessfulResultWithValue()
    {
        // Arrange
        var expectedValue = "test value";

        // Act
        var result = Result.Success(expectedValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedValue);
        result.ErrorType.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void FailureGeneric_CreatesFailedResultWithoutValue()
    {
        // Arrange
        var errorType = ErrorType.NotFound;
        var errorMessage = "Resource not found";

        // Act
        var result = Result.Failure<string>(errorType, errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorType.Should().Be(errorType);
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Theory]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.Internal)]
    public void Failure_SupportsAllErrorTypes(ErrorType errorType)
    {
        // Act
        var result = Result.Failure(errorType, "Test error");

        // Assert
        result.ErrorType.Should().Be(errorType);
    }

    [Fact]
    public void SuccessGeneric_SupportsReferenceTypes()
    {
        // Arrange
        var obj = new TestObject { Id = 42, Name = "Test" };

        // Act
        var result = Result.Success(obj);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(obj);
        result.Value!.Id.Should().Be(42);
        result.Value.Name.Should().Be("Test");
    }

    [Fact]
    public void SuccessGeneric_SupportsValueTypes()
    {
        // Arrange
        var number = 42;

        // Act
        var result = Result.Success(number);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(number);
    }

    private sealed class TestObject
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
