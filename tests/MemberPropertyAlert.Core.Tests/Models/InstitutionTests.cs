using AutoFixture;
using FluentAssertions;
using MemberPropertyAlert.Core.Models;
using Xunit;

namespace MemberPropertyAlert.Core.Tests.Models;

public class InstitutionTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Institution_ShouldHaveCorrectProperties()
    {
        // Arrange
        var institution = _fixture.Create<Institution>();

        // Assert
        institution.Should().NotBeNull();
        institution.Id.Should().NotBeNullOrEmpty();
        institution.Name.Should().NotBeNullOrEmpty();
        institution.ContactEmail.Should().NotBeNullOrEmpty();
        institution.CreatedAt.Should().BeAfter(DateTime.MinValue);
    }

    [Fact]
    public void Institution_WithValidData_ShouldBeValid()
    {
        // Arrange & Act
        var institution = new Institution
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Institution",
            ContactEmail = "test@example.com",
            WebhookUrl = "https://example.com/webhook",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        institution.Id.Should().NotBeNullOrEmpty();
        institution.Name.Should().Be("Test Institution");
        institution.ContactEmail.Should().Be("test@example.com");
        institution.WebhookUrl.Should().Be("https://example.com/webhook");
        institution.IsActive.Should().BeTrue();
        institution.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        institution.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Institution_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var institution = new Institution();

        // Assert
        institution.Id.Should().NotBeNullOrEmpty(); // Guid.NewGuid().ToString() is called
        institution.Name.Should().Be(string.Empty); // Default value
        institution.ContactEmail.Should().Be(string.Empty); // Default value
        institution.WebhookUrl.Should().BeNull();
        institution.NotificationSettings.Should().NotBeNull(); // new() is called
        institution.IsActive.Should().BeTrue(); // Default is true
        institution.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1)); // DateTime.UtcNow is called
        institution.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1)); // DateTime.UtcNow is called
    }
}
