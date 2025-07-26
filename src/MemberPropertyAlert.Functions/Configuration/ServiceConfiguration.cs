using System.ComponentModel.DataAnnotations;

namespace MemberPropertyAlert.Functions.Configuration
{
    /// <summary>
    /// Circuit breaker configuration for external service calls
    /// </summary>
    public class CircuitBreakerConfiguration
    {
        [Range(1, 100)]
        public int FailureThreshold { get; set; } = 5;
        
        public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromMinutes(1);
        
        [Range(1, 1000)]
        public int MinimumThroughput { get; set; } = 10;
        
        public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Retry policy configuration for resilient HTTP calls
    /// </summary>
    public class RetryPolicyConfiguration
    {
        [Range(1, 10)]
        public int MaxRetryAttempts { get; set; } = 3;
        
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);
        
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Health check configuration for external dependencies
    /// </summary>
    public class HealthCheckConfiguration
    {
        public MockRentCastApiHealthCheck MockRentCastApi { get; set; } = new();
    }

    public class MockRentCastApiHealthCheck
    {
        [Required]
        public string Url { get; set; } = string.Empty;
        
        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = 10;
        
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Extended RentCast configuration with additional resilience settings
    /// </summary>
    public class ExtendedRentCastConfiguration
    {
        [Required]
        public string BaseUrl { get; set; } = string.Empty;
        
        [Required]
        public string ApiKey { get; set; } = string.Empty;
        
        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = 30;
        
        [Range(1, 10)]
        public int MaxRetries { get; set; } = 3;
        
        [Range(100, 10000)]
        public int RateLimitDelayMs { get; set; } = 1000;
        
        public bool EnableCaching { get; set; } = true;
        
        [Range(1, 1440)]
        public int CacheDurationMinutes { get; set; } = 15;
        
        public bool UseMockService { get; set; } = false;
    }
}
