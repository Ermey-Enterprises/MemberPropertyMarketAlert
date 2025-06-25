using FluentAssertions;
using MemberPropertyAlert.Core.Common;
using Xunit;

namespace MemberPropertyAlert.Core.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        const string errorMessage = "Something went wrong";

        // Act
        var result = Result.Failure(errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(errorMessage);
    }

    [Fact]
    public void Failure_WithGenericType_ShouldCreateFailedResult()
    {
        // Arrange
        const string errorMessage = "Something went wrong";

        // Act
        Result<string> result = Result.Failure<string>(errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(errorMessage);
    }

    [Fact]
    public void Value_OnFailedResult_ShouldThrowInvalidOperationException()
    {
        // Arrange
        Result<string> result = Result.Failure<string>("Error");

        // Act & Assert
        var action = () => result.Value;
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot access value of failed result");
    }

    [Fact]
    public void Success_WithNullValue_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => Result.Success<string>(null!);
        action.Should().Throw<ArgumentNullException>();
    }
}
