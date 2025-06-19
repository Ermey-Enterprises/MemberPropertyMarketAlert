using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MemberPropertyMarketAlert.Core.Models;
using MemberPropertyMarketAlert.Core.Services;

namespace MemberPropertyMarketAlert.Tests;

public class PropertyMatchingServiceTests
{
    private readonly Mock<ICosmosDbService> _mockCosmosDbService;
    private readonly Mock<ILogger<PropertyMatchingService>> _mockLogger;
    private readonly PropertyMatchingService _propertyMatchingService;

    public PropertyMatchingServiceTests()
    {
        _mockCosmosDbService = new Mock<ICosmosDbService>();
        _mockLogger = new Mock<ILogger<PropertyMatchingService>>();
        _propertyMatchingService = new PropertyMatchingService(_mockCosmosDbService.Object, _mockLogger.Object);
    }

    [Fact]
    public void CalculateMatch_ExactAddressMatch_ReturnsExactMatch()
    {
        // Arrange
        var memberAddress = new MemberAddress
        {
            Id = "member1",
            Address = "123 Main St",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345"
        };

        var propertyListing = new PropertyListing
        {
            Id = "listing1",
            Address = "123 Main St",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345"
        };

        // Act
        var result = _propertyMatchingService.CalculateMatch(memberAddress, propertyListing);

        // Assert
        Assert.True(result.IsMatch);
        Assert.Equal(MatchConfidence.Exact, result.Confidence);
        Assert.Equal(100.0, result.Score);
        Assert.Equal(MatchMethod.ExactAddress, result.Method);
    }

    [Fact]
    public void CalculateMatch_NormalizedAddressMatch_ReturnsExactMatch()
    {
        // Arrange
        var memberAddress = new MemberAddress
        {
            Id = "member1",
            Address = "123 Main Street",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345"
        };
        memberAddress.UpdateNormalizedAddress();

        var propertyListing = new PropertyListing
        {
            Id = "listing1",
            Address = "123 Main St",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345"
        };
        propertyListing.UpdateNormalizedAddress();

        // Act
        var result = _propertyMatchingService.CalculateMatch(memberAddress, propertyListing);

        // Assert
        Assert.True(result.IsMatch);
        Assert.Equal(MatchConfidence.Exact, result.Confidence);
        Assert.Equal(100.0, result.Score);
        Assert.Equal(MatchMethod.NormalizedAddress, result.Method);
    }

    [Fact]
    public void CalculateMatch_HighConfidenceFuzzyMatch_ReturnsHighConfidenceMatch()
    {
        // Arrange
        var memberAddress = new MemberAddress
        {
            Id = "member1",
            Address = "123 Main Street",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345"
        };

        var propertyListing = new PropertyListing
        {
            Id = "listing1",
            Address = "123 Main St.",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345"
        };

        // Act
        var result = _propertyMatchingService.CalculateMatch(memberAddress, propertyListing);

        // Assert
        Assert.True(result.IsMatch);
        Assert.True(result.Confidence >= MatchConfidence.Medium);
        Assert.True(result.Score >= 85.0);
        Assert.Equal(MatchMethod.FuzzyMatch, result.Method);
    }

    [Fact]
    public void CalculateMatch_NoMatch_ReturnsNoMatch()
    {
        // Arrange
        var memberAddress = new MemberAddress
        {
            Id = "member1",
            Address = "123 Main St",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345"
        };

        var propertyListing = new PropertyListing
        {
            Id = "listing1",
            Address = "456 Oak Ave",
            City = "Somewhere",
            State = "NY",
            ZipCode = "67890"
        };

        // Act
        var result = _propertyMatchingService.CalculateMatch(memberAddress, propertyListing);

        // Assert
        Assert.False(result.IsMatch);
        Assert.Equal(MatchConfidence.Low, result.Confidence);
        Assert.True(result.Score < 75.0);
    }

    [Fact]
    public async Task FindMatchesForInstitutionAsync_WithMatches_ReturnsMatches()
    {
        // Arrange
        var institutionId = "institution1";
        var memberAddresses = new List<MemberAddress>
        {
            new MemberAddress
            {
                Id = "member1",
                InstitutionId = institutionId,
                AnonymousReferenceId = "ref1",
                Address = "123 Main St",
                City = "Anytown",
                State = "CA",
                ZipCode = "12345"
            }
        };

        var propertyListings = new List<PropertyListing>
        {
            new PropertyListing
            {
                Id = "listing1",
                Address = "123 Main St",
                City = "Anytown",
                State = "CA",
                ZipCode = "12345",
                Price = 500000,
                ListingDate = DateTime.UtcNow,
                Status = PropertyStatus.Active
            }
        };

        _mockCosmosDbService
            .Setup(x => x.GetMemberAddressesByInstitutionAsync(institutionId))
            .ReturnsAsync(memberAddresses);

        // Act
        var result = await _propertyMatchingService.FindMatchesForInstitutionAsync(institutionId, propertyListings);

        // Assert
        Assert.Single(result);
        var match = result.First();
        Assert.Equal("member1", match.MemberAddressId);
        Assert.Equal("listing1", match.PropertyListingId);
        Assert.Equal(institutionId, match.InstitutionId);
        Assert.Equal("ref1", match.AnonymousReferenceId);
        Assert.Equal(MatchConfidence.Exact, match.MatchConfidence);
    }

    [Fact]
    public async Task FindMatchesForInstitutionAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var institutionId = "institution1";
        var memberAddresses = new List<MemberAddress>
        {
            new MemberAddress
            {
                Id = "member1",
                InstitutionId = institutionId,
                AnonymousReferenceId = "ref1",
                Address = "123 Main St",
                City = "Anytown",
                State = "CA",
                ZipCode = "12345"
            }
        };

        var propertyListings = new List<PropertyListing>
        {
            new PropertyListing
            {
                Id = "listing1",
                Address = "456 Oak Ave",
                City = "Somewhere",
                State = "NY",
                ZipCode = "67890",
                Price = 500000,
                ListingDate = DateTime.UtcNow,
                Status = PropertyStatus.Active
            }
        };

        _mockCosmosDbService
            .Setup(x => x.GetMemberAddressesByInstitutionAsync(institutionId))
            .ReturnsAsync(memberAddresses);

        // Act
        var result = await _propertyMatchingService.FindMatchesForInstitutionAsync(institutionId, propertyListings);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CalculateMatch_GeographicProximity_ReturnsProximityMatch()
    {
        // Arrange
        var memberAddress = new MemberAddress
        {
            Id = "member1",
            Address = "123 Main St",
            City = "Anytown",
            State = "CA",
            ZipCode = "12345",
            Latitude = 37.7749,
            Longitude = -122.4194
        };

        var propertyListing = new PropertyListing
        {
            Id = "listing1",
            Address = "124 Main St", // Very close address
            City = "Anytown",
            State = "CA",
            ZipCode = "12345",
            Latitude = 37.7750, // Very close coordinates
            Longitude = -122.4195
        };

        // Act
        var result = _propertyMatchingService.CalculateMatch(memberAddress, propertyListing);

        // Assert
        Assert.True(result.IsMatch);
        Assert.True(result.Method == MatchMethod.GeographicProximity || result.Method == MatchMethod.FuzzyMatch);
    }
}
