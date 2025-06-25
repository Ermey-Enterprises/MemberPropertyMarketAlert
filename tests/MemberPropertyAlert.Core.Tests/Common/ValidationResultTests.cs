using FluentAssertions;
using MemberPropertyAlert.Core.Common;
using Xunit;

namespace MemberPropertyAlert.Core.Tests.Common;

public class ValidationResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulValidationResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeEmpty();
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_WithValidationErrors_ShouldCreateFailedResult()
    {
        // Arrange
        var validationErrors = new List<ValidationError>
        {
            new() { PropertyName = "Name", ErrorMessage = "Name is required", AttemptedValue = "" },
            new() { PropertyName = "Email", ErrorMessage = "Email is invalid", AttemptedValue = "invalid-email" }
        };

        // Act
        var result = ValidationResult.Failure(validationErrors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.ValidationErrors.Should().HaveCount(2);
        result.ValidationErrors.Should().Contain(e => e.PropertyName == "Name");
        result.ValidationErrors.Should().Contain(e => e.PropertyName == "Email");
        result.Error.Should().Contain("Name: Name is required");
        result.Error.Should().Contain("Email: Email is invalid");
    }

    [Fact]
    public void Failure_WithSingleError_ShouldCreateFailedResult()
    {
        // Arrange
        const string propertyName = "Name";
        const string errorMessage = "Name is required";
        const string attemptedValue = "";

        // Act
        var result = ValidationResult.Failure(propertyName, errorMessage, attemptedValue);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.ValidationErrors.Should().HaveCount(1);
        
        var validationError = result.ValidationErrors.First();
        validationError.PropertyName.Should().Be(propertyName);
        validationError.ErrorMessage.Should().Be(errorMessage);
        validationError.AttemptedValue.Should().Be(attemptedValue);
        
        result.Error.Should().Be($"{propertyName}: {errorMessage}");
    }

    [Fact]
    public void ValidationError_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var validationError = new ValidationError
        {
            PropertyName = "TestProperty",
            ErrorMessage = "Test error message",
            AttemptedValue = "test value"
        };

        // Assert
        validationError.PropertyName.Should().Be("TestProperty");
        validationError.ErrorMessage.Should().Be("Test error message");
        validationError.AttemptedValue.Should().Be("test value");
    }

    [Fact]
    public void ValidationError_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var validationError = new ValidationError();

        // Assert
        validationError.PropertyName.Should().BeEmpty();
        validationError.ErrorMessage.Should().BeEmpty();
        validationError.AttemptedValue.Should().BeNull(); // Default value is null
    }
}
