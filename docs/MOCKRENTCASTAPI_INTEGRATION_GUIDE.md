# MockRentCastAPI Integration Guide

## Overview

This document describes the complete integration of the MemberPropertyMarketAlert system with the external MockRentCastAPI service, implementing industry-standard resilience patterns and best practices.

## Architecture

### System Components

1. **MemberPropertyMarketAlert.Functions** - Main Azure Functions application
2. **MockRentCastAPI** - External mock service running on `http://localhost:5000`
3. **Enhanced Integration Layer** - Resilient HTTP client with retry, circuit breaker, and caching

### Integration Patterns Implemented

#### 🔄 **Resilience Patterns**
- **Retry Policy**: Exponential backoff with jitter
- **Circuit Breaker**: Prevents cascade failures
- **Timeout Handling**: Configurable request timeouts
- **Bulkhead Pattern**: Resource isolation

#### 📊 **Observability**
- **Health Checks**: External dependency monitoring
- **Structured Logging**: Comprehensive request/response logging
- **Metrics**: Performance and error tracking
- **Distributed Tracing**: End-to-end request tracking

#### ⚡ **Performance**
- **Memory Caching**: Configurable response caching
- **Connection Pooling**: Efficient HTTP client usage
- **Rate Limiting**: Respectful API consumption

## Configuration

### Local Development Settings

The integration is configured via `local.settings.json`:

```json
{
  "Values": {
    "RentCast__BaseUrl": "http://localhost:5000/v1",
    "RentCast__ApiKey": "test-api-key-123",
    "RentCast__TimeoutSeconds": "30",
    "RentCast__MaxRetries": "3",
    "RentCast__RateLimitDelayMs": "500",
    "RentCast__EnableCaching": "true",
    "RentCast__CacheDurationMinutes": "15",
    
    "CircuitBreaker__FailureThreshold": "5",
    "CircuitBreaker__SamplingDuration": "00:01:00",
    "CircuitBreaker__DurationOfBreak": "00:00:30",
    
    "RetryPolicy__MaxRetryAttempts": "3",
    "RetryPolicy__BaseDelay": "00:00:02",
    "RetryPolicy__MaxDelay": "00:00:30",
    
    "HealthCheck__MockRentCastApi__Url": "http://localhost:5000/health",
    "HealthCheck__MockRentCastApi__TimeoutSeconds": "10",
    "HealthCheck__MockRentCastApi__Enabled": "true"
  }
}
```

### Production Configuration

For production environments, these settings should be stored in:
- **Azure Key Vault** for sensitive values (API keys)
- **Application Settings** for non-sensitive configuration
- **Environment Variables** for deployment-specific values

## Service Architecture

### Enhanced RentCast Service

The `EnhancedRentCastService` implements the `IRentCastService` interface with:

```csharp
public class EnhancedRentCastService : IRentCastService
{
    // Implements all IRentCastService methods with:
    // - Resilience patterns (retry, circuit breaker)
    // - Caching for performance
    // - Comprehensive error handling
    // - Structured logging
}
```

### Key Features

#### 1. **Retry Policy**
- Exponential backoff: 2s, 4s, 8s, 16s, 30s (max)
- Retries on: HTTP errors, timeouts, 5xx responses
- Configurable retry attempts and delays

#### 2. **Circuit Breaker**
- Opens after 5 consecutive failures
- Stays open for 30 seconds
- Prevents unnecessary load on failing services

#### 3. **Caching Strategy**
- **Property Listings**: 15 minutes cache
- **Recent Listings**: 7.5 minutes cache (more volatile)
- **State Listings**: 30 minutes cache (less volatile)
- Memory-based with configurable expiration

#### 4. **Health Monitoring**
- Dedicated health check for MockRentCastAPI
- Monitors response time and availability
- Integrates with Azure Functions health system

## API Endpoints

### MockRentCastAPI Endpoints

The integration supports all MockRentCastAPI endpoints:

#### **Properties**
- `GET /v1/properties?address={address}` - Get property by address

#### **Listings**
- `GET /v1/listings/sale?city={city}&state={state}&daysBack={days}` - Recent sales
- `GET /v1/listings/sale?state={state}&listedSince={date}` - New listings
- `GET /v1/listings/rental` - Rental listings

#### **Health**
- `GET /health` - Basic health check
- `GET /health/detailed` - Detailed health information

### Response Models

The service handles MockRentCastAPI response models:

```csharp
public class RentCastApiResponse
{
    public List<RentCastProperty> Properties { get; set; }
    public int Count { get; set; }
    public string Status { get; set; }
}

public class RentCastProperty
{
    public string Id { get; set; }
    public string FormattedAddress { get; set; }
    public string PropertyType { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? SquareFeet { get; set; }
    public RentCastForSaleInfo ForSale { get; set; }
    // ... additional properties
}
```

## Error Handling

### Error Categories

1. **Transient Errors** (Retried)
   - Network timeouts
   - HTTP 5xx responses
   - Connection failures

2. **Permanent Errors** (Not Retried)
   - HTTP 4xx responses (except 408, 429)
   - Authentication failures
   - Malformed requests

3. **Circuit Breaker Scenarios**
   - Multiple consecutive failures
   - Service unavailability
   - Degraded performance

### Error Response Handling

```csharp
try
{
    var response = await _combinedPolicy.ExecuteAsync(async () =>
        await _httpClient.GetAsync(endpoint));
    
    // Handle successful response
}
catch (BrokenCircuitException ex)
{
    _logger.LogError(ex, "Circuit breaker is open for RentCast API");
    return null; // Graceful degradation
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "HTTP request failed");
    return null;
}
```

## Deployment Guide

### Prerequisites

1. **MockRentCastAPI Running**
   ```bash
   cd MockRentCastAPI
   dotnet run
   ```
   Service should be available at `http://localhost:5000`

2. **Configuration Updated**
   - Verify `local.settings.json` points to correct MockRentCastAPI URL
   - Ensure API key matches MockRentCastAPI configuration

### Local Development

1. **Start MockRentCastAPI**
   ```bash
   cd MockRentCastAPI
   dotnet run
   ```

2. **Start MemberPropertyMarketAlert Functions**
   ```bash
   cd MemberPropertyMarketAlert/src/MemberPropertyAlert.Functions
   func start
   ```

3. **Verify Integration**
   - Check health endpoint: `http://localhost:7071/api/health`
   - Test property lookup via Functions API
   - Monitor logs for successful MockRentCastAPI calls

### Production Deployment

1. **Deploy MockRentCastAPI**
   - Deploy to Azure Container Instances or App Service
   - Configure production URL in Functions app settings

2. **Update Configuration**
   - Set `RentCast__BaseUrl` to production MockRentCastAPI URL
   - Store API keys in Azure Key Vault
   - Configure monitoring and alerting

## Monitoring and Observability

### Health Checks

Access health information:
- **Functions Health**: `GET /api/health`
- **MockRentCastAPI Health**: `GET http://localhost:5000/health`

### Logging

The integration provides structured logging:

```csharp
_logger.LogInformation("Getting property listing for address: {Address}", address);
_logger.LogWarning("RentCast API returned {StatusCode} for address {Address}", 
    response.StatusCode, address);
_logger.LogError(ex, "Circuit breaker is open for RentCast API. Address: {Address}", address);
```

### Metrics

Key metrics to monitor:
- **Request Success Rate**: Percentage of successful API calls
- **Response Time**: Average and P95 response times
- **Circuit Breaker State**: Open/Closed status
- **Cache Hit Rate**: Effectiveness of caching strategy
- **Error Rate**: Frequency and types of errors

## Testing

### Unit Tests

Test the enhanced service with mocked dependencies:

```csharp
[Test]
public async Task GetPropertyListingAsync_WithValidAddress_ReturnsListing()
{
    // Arrange
    var mockHttpClient = new Mock<HttpClient>();
    var service = new EnhancedRentCastService(mockHttpClient.Object, ...);
    
    // Act
    var result = await service.GetPropertyListingAsync("123 Main St");
    
    // Assert
    Assert.IsNotNull(result);
}
```

### Integration Tests

Test against running MockRentCastAPI:

```csharp
[Test]
public async Task Integration_GetPropertyListing_ReturnsValidData()
{
    // Requires MockRentCastAPI running on localhost:5000
    var service = CreateRealService();
    var result = await service.GetPropertyListingAsync("123 Main St, Austin, TX 78701");
    
    Assert.IsNotNull(result);
    Assert.IsNotEmpty(result.Address);
}
```

### Load Testing

Use tools like NBomber or Artillery to test:
- Concurrent request handling
- Circuit breaker behavior under load
- Cache effectiveness
- Performance under stress

## Troubleshooting

### Common Issues

1. **Connection Refused**
   - Verify MockRentCastAPI is running
   - Check URL configuration
   - Verify firewall settings

2. **Authentication Errors**
   - Verify API key configuration
   - Check MockRentCastAPI authentication settings

3. **Timeout Issues**
   - Increase timeout configuration
   - Check network connectivity
   - Monitor MockRentCastAPI performance

4. **Circuit Breaker Open**
   - Check MockRentCastAPI health
   - Review error logs
   - Wait for circuit breaker to reset

### Diagnostic Commands

```bash
# Check MockRentCastAPI health
curl http://localhost:5000/health

# Test property endpoint
curl "http://localhost:5000/v1/properties?address=123%20Main%20St"

# Check Functions health
curl http://localhost:7071/api/health
```

## Performance Optimization

### Caching Strategy

- **Cache Duration**: Adjust based on data volatility
- **Cache Size**: Monitor memory usage
- **Cache Keys**: Ensure uniqueness and consistency

### HTTP Client Optimization

- **Connection Pooling**: Reuse connections
- **Timeout Configuration**: Balance responsiveness vs reliability
- **Compression**: Enable if supported by MockRentCastAPI

### Resource Management

- **Memory Usage**: Monitor cache size and object lifecycle
- **CPU Usage**: Optimize serialization and processing
- **Network Usage**: Minimize unnecessary requests

## Security Considerations

### API Key Management

- Store in Azure Key Vault for production
- Rotate keys regularly
- Monitor for unauthorized usage

### Network Security

- Use HTTPS in production
- Implement proper firewall rules
- Consider VNet integration for Azure deployments

### Data Protection

- Sanitize logs to avoid PII exposure
- Implement proper error handling
- Follow data retention policies

## Future Enhancements

### Planned Improvements

1. **Advanced Caching**
   - Redis cache for distributed scenarios
   - Cache invalidation strategies
   - Cache warming

2. **Enhanced Monitoring**
   - Custom metrics and dashboards
   - Alerting rules
   - Performance baselines

3. **Scalability**
   - Horizontal scaling support
   - Load balancing
   - Auto-scaling policies

4. **Security**
   - OAuth 2.0 integration
   - Certificate-based authentication
   - API rate limiting

## Conclusion

The MockRentCastAPI integration provides a robust, production-ready foundation for external API consumption with comprehensive resilience patterns, monitoring, and performance optimization. The implementation follows industry best practices and provides a solid foundation for scaling and extending the system.
