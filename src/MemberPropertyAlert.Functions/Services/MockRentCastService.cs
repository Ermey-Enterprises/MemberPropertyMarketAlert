using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;

namespace MemberPropertyAlert.Functions.Services
{
    public class MockRentCastService : IRentCastService
    {
        private readonly ILogger<MockRentCastService> _logger;
        private readonly MockRentCastConfiguration _config;
        private readonly List<MockPropertyData> _mockProperties;
        private readonly Random _random;

        public MockRentCastService(
            ILogger<MockRentCastService> logger,
            IOptions<MockRentCastConfiguration> config)
        {
            _logger = logger;
            _config = config.Value;
            _random = new Random();
            _mockProperties = GenerateMockProperties();
        }

        public async Task<PropertyListing?> GetPropertyListingAsync(string address)
        {
            _logger.LogInformation("Mock: Getting property listing for address: {Address}", address);

            // Simulate API delay
            await Task.Delay(_config.SimulatedDelayMs);

            // Simulate occasional failures
            if (_random.NextDouble() < _config.FailureRate)
            {
                _logger.LogWarning("Mock: Simulated API failure for address {Address}", address);
                return null;
            }

            // Find or generate a mock property for this address
            var mockProperty = FindOrCreateMockProperty(address);
            
            return ConvertToPropertyListing(mockProperty);
        }

        public async Task<bool> IsPropertyListedAsync(string address)
        {
            var listing = await GetPropertyListingAsync(address);
            return listing?.IsActive == true;
        }

        public async Task<PropertyListing[]> GetRecentListingsAsync(string city, string state, int daysBack = 7)
        {
            _logger.LogInformation("Mock: Getting recent listings for {City}, {State} (last {Days} days)", city, state, daysBack);

            await Task.Delay(_config.SimulatedDelayMs);

            if (_random.NextDouble() < _config.FailureRate)
            {
                _logger.LogWarning("Mock: Simulated API failure for recent listings");
                return Array.Empty<PropertyListing>();
            }

            // Generate mock recent listings
            var count = _random.Next(5, 25);
            var listings = new List<PropertyListing>();

            for (int i = 0; i < count; i++)
            {
                var mockProperty = GenerateRandomMockProperty(city, state);
                mockProperty.ListDate = DateTime.UtcNow.AddDays(-_random.Next(0, daysBack));
                listings.Add(ConvertToPropertyListing(mockProperty));
            }

            return listings.ToArray();
        }

        public async Task<PropertyListing[]> GetStateListingsAsync(string state)
        {
            _logger.LogInformation("Mock: Getting all listings for state: {State}", state);

            await Task.Delay(_config.SimulatedDelayMs * 2); // Longer delay for state-wide search

            if (_random.NextDouble() < _config.FailureRate)
            {
                _logger.LogWarning("Mock: Simulated API failure for state listings");
                return Array.Empty<PropertyListing>();
            }

            // Generate mock state listings
            var count = _random.Next(50, 200);
            var listings = new List<PropertyListing>();

            for (int i = 0; i < count; i++)
            {
                var city = GetRandomCityForState(state);
                var mockProperty = GenerateRandomMockProperty(city, state);
                listings.Add(ConvertToPropertyListing(mockProperty));
            }

            return listings.ToArray();
        }

        public async Task<PropertyListing[]> GetNewListingsAsync(string state, DateTime sinceDate)
        {
            _logger.LogInformation("Mock: Getting new listings for state {State} since {Since}", state, sinceDate);

            await Task.Delay(_config.SimulatedDelayMs);

            if (_random.NextDouble() < _config.FailureRate)
            {
                _logger.LogWarning("Mock: Simulated API failure for new listings");
                return Array.Empty<PropertyListing>();
            }

            var daysSince = (DateTime.UtcNow - sinceDate).Days;
            var count = _random.Next(10, Math.Max(10, daysSince * 5));
            var listings = new List<PropertyListing>();

            for (int i = 0; i < count; i++)
            {
                var city = GetRandomCityForState(state);
                var mockProperty = GenerateRandomMockProperty(city, state);
                mockProperty.ListDate = sinceDate.AddDays(_random.NextDouble() * daysSince);
                listings.Add(ConvertToPropertyListing(mockProperty));
            }

            return listings.ToArray();
        }

        private List<MockPropertyData> GenerateMockProperties()
        {
            var properties = new List<MockPropertyData>();
            
            // Generate some consistent mock properties for testing
            var addresses = new[]
            {
                "123 Main St",
                "456 Oak Ave",
                "789 Pine Rd",
                "321 Elm St",
                "654 Maple Dr",
                "987 Cedar Ln",
                "147 Birch Way",
                "258 Spruce Ct",
                "369 Willow Blvd",
                "741 Aspen Pl"
            };

            var cities = new[] { "Austin", "Dallas", "Houston", "San Antonio", "Fort Worth" };
            var propertyTypes = new[] { "Single Family", "Townhouse", "Condo", "Duplex" };

            foreach (var address in addresses)
            {
                foreach (var city in cities)
                {
                    properties.Add(new MockPropertyData
                    {
                        Id = Guid.NewGuid().ToString(),
                        Address = address,
                        City = city,
                        State = "TX",
                        ZipCode = GenerateRandomZipCode(),
                        PropertyType = propertyTypes[_random.Next(propertyTypes.Length)],
                        Bedrooms = _random.Next(1, 6),
                        Bathrooms = _random.Next(1, 4),
                        SquareFeet = _random.Next(800, 4000),
                        Price = _random.Next(150000, 800000),
                        IsListed = _random.NextDouble() > 0.3, // 70% chance of being listed
                        ListDate = DateTime.UtcNow.AddDays(-_random.Next(1, 180)),
                        DaysOnMarket = _random.Next(1, 120),
                        Status = GetRandomStatus(),
                        Description = GenerateRandomDescription(),
                        Photos = GenerateRandomPhotos()
                    });
                }
            }

            return properties;
        }

        private MockPropertyData FindOrCreateMockProperty(string address)
        {
            // Try to find existing mock property
            var existing = _mockProperties.FirstOrDefault(p => 
                p.Address.Equals(ExtractStreetAddress(address), StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return existing;
            }

            // Create new mock property
            var parts = address.Split(',');
            var streetAddress = parts.Length > 0 ? parts[0].Trim() : address;
            var city = parts.Length > 1 ? parts[1].Trim() : "Austin";
            var state = parts.Length > 2 ? ExtractState(parts[2].Trim()) : "TX";

            return GenerateRandomMockProperty(city, state, streetAddress);
        }

        private MockPropertyData GenerateRandomMockProperty(string city, string state, string? streetAddress = null)
        {
            var propertyTypes = new[] { "Single Family", "Townhouse", "Condo", "Duplex" };
            
            return new MockPropertyData
            {
                Id = Guid.NewGuid().ToString(),
                Address = streetAddress ?? GenerateRandomAddress(),
                City = city,
                State = state,
                ZipCode = GenerateRandomZipCode(),
                PropertyType = propertyTypes[_random.Next(propertyTypes.Length)],
                Bedrooms = _random.Next(1, 6),
                Bathrooms = _random.Next(1, 4),
                SquareFeet = _random.Next(800, 4000),
                Price = _random.Next(150000, 800000),
                IsListed = _random.NextDouble() > 0.2, // 80% chance of being listed
                ListDate = DateTime.UtcNow.AddDays(-_random.Next(1, 180)),
                DaysOnMarket = _random.Next(1, 120),
                Status = GetRandomStatus(),
                Description = GenerateRandomDescription(),
                Photos = GenerateRandomPhotos()
            };
        }

        private PropertyListing ConvertToPropertyListing(MockPropertyData mockProperty)
        {
            return new PropertyListing
            {
                Id = mockProperty.Id,
                Address = mockProperty.Address,
                City = mockProperty.City,
                State = mockProperty.State,
                ZipCode = mockProperty.ZipCode,
                Price = mockProperty.Price,
                Bedrooms = mockProperty.Bedrooms,
                Bathrooms = mockProperty.Bathrooms,
                SquareFootage = mockProperty.SquareFeet,
                ListingDate = mockProperty.ListDate,
                DaysOnMarket = mockProperty.DaysOnMarket,
                PropertyType = mockProperty.PropertyType,
                MlsNumber = $"MLS{_random.Next(100000, 999999)}",
                ListingAgent = GenerateRandomAgentName(),
                Description = mockProperty.Description,
                Photos = mockProperty.Photos,
                Status = mockProperty.Status,
                // IsActive is calculated from other properties
                LastUpdated = DateTime.UtcNow
            };
        }

        private string ExtractStreetAddress(string fullAddress)
        {
            var parts = fullAddress.Split(',');
            return parts.Length > 0 ? parts[0].Trim() : fullAddress;
        }

        private string ExtractState(string stateZip)
        {
            var parts = stateZip.Trim().Split(' ');
            return parts.Length > 0 ? parts[0].Trim() : "TX";
        }

        private string GenerateRandomAddress()
        {
            var streetNumbers = new[] { "123", "456", "789", "321", "654", "987", "147", "258", "369", "741" };
            var streetNames = new[] { "Main", "Oak", "Pine", "Elm", "Maple", "Cedar", "Birch", "Spruce", "Willow", "Aspen" };
            var streetTypes = new[] { "St", "Ave", "Rd", "Dr", "Ln", "Way", "Ct", "Blvd", "Pl" };

            return $"{streetNumbers[_random.Next(streetNumbers.Length)]} {streetNames[_random.Next(streetNames.Length)]} {streetTypes[_random.Next(streetTypes.Length)]}";
        }

        private string GenerateRandomZipCode()
        {
            return _random.Next(10000, 99999).ToString();
        }

        private string GetRandomStatus()
        {
            var statuses = new[] { "Active", "Under Contract", "Pending", "Sold" };
            return statuses[_random.Next(statuses.Length)];
        }

        private string GenerateRandomDescription()
        {
            var descriptions = new[]
            {
                "Beautiful home in a quiet neighborhood with modern amenities.",
                "Spacious property with updated kitchen and bathrooms.",
                "Charming house with large backyard and great curb appeal.",
                "Move-in ready home with hardwood floors throughout.",
                "Recently renovated property in desirable location.",
                "Cozy home with open floor plan and natural light.",
                "Well-maintained property with mature landscaping.",
                "Updated home with energy-efficient features."
            };

            return descriptions[_random.Next(descriptions.Length)];
        }

        private List<string> GenerateRandomPhotos()
        {
            var photoCount = _random.Next(3, 8);
            var photos = new List<string>();

            for (int i = 0; i < photoCount; i++)
            {
                photos.Add($"https://picsum.photos/800/600?random={_random.Next(1000, 9999)}");
            }

            return photos;
        }

        private string GenerateRandomAgentName()
        {
            var firstNames = new[] { "John", "Jane", "Mike", "Sarah", "David", "Lisa", "Chris", "Amy", "Robert", "Jennifer" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };

            return $"{firstNames[_random.Next(firstNames.Length)]} {lastNames[_random.Next(lastNames.Length)]}";
        }

        private string GetRandomCityForState(string state)
        {
            var citiesByState = new Dictionary<string, string[]>
            {
                ["TX"] = new[] { "Austin", "Dallas", "Houston", "San Antonio", "Fort Worth", "El Paso", "Arlington", "Corpus Christi" },
                ["CA"] = new[] { "Los Angeles", "San Francisco", "San Diego", "Sacramento", "San Jose", "Fresno", "Long Beach", "Oakland" },
                ["FL"] = new[] { "Miami", "Tampa", "Orlando", "Jacksonville", "St. Petersburg", "Hialeah", "Tallahassee", "Fort Lauderdale" },
                ["NY"] = new[] { "New York", "Buffalo", "Rochester", "Yonkers", "Syracuse", "Albany", "New Rochelle", "Mount Vernon" }
            };

            if (citiesByState.ContainsKey(state.ToUpper()))
            {
                var cities = citiesByState[state.ToUpper()];
                return cities[_random.Next(cities.Length)];
            }

            return "Unknown City";
        }
    }

    public class MockPropertyData
    {
        public string Id { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string PropertyType { get; set; } = string.Empty;
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public int SquareFeet { get; set; }
        public decimal Price { get; set; }
        public bool IsListed { get; set; }
        public DateTime ListDate { get; set; }
        public int DaysOnMarket { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Photos { get; set; } = new();
    }

    public class MockRentCastConfiguration
    {
        public int SimulatedDelayMs { get; set; } = 500;
        public double FailureRate { get; set; } = 0.05; // 5% failure rate
        public bool EnableRandomData { get; set; } = true;
        public int MaxPropertiesPerRequest { get; set; } = 100;
    }
}
