# CI/CD Integration Guide for Testing and Administration Enhancements

This document explains how the new testing and administration enhancements integrate with the existing CI/CD pipeline and provides guidance for deployment and testing workflows.

## Overview

The enhanced CI/CD pipeline now supports:

1. **Multi-Project Build System** - Builds all components including mock services
2. **Node.js Integration** - Handles React UI build process
3. **Mock Service Testing** - Automated testing with mock webhook client
4. **Enhanced Integration Tests** - Comprehensive testing including webhook flows
5. **Artifact Management** - Proper handling of all build outputs

## Pipeline Architecture

### Build and Test Job Enhancements

#### Node.js Integration
```yaml
- name: Setup Node.js
  uses: actions/setup-node@v4
  with:
    node-version: ${{ env.NODE_VERSION }}
    cache: 'npm'
    cache-dependency-path: '${{ env.AZURE_WEBAPP_PACKAGE_PATH }}/src/package-lock.json'

- name: Install UI dependencies
  working-directory: ${{ env.AZURE_WEBAPP_PACKAGE_PATH }}/src
  run: npm ci

- name: Build UI
  working-directory: ${{ env.AZURE_WEBAPP_PACKAGE_PATH }}/src
  run: npm run build
```

**Benefits:**
- Automatic caching of Node.js dependencies
- Consistent build environment across all runs
- Proper handling of React build process

#### Enhanced Artifact Management
The pipeline now creates artifacts for all components:

1. **Function App** (`function-app`) - Azure Functions backend
2. **Web App** (`web-app`) - React UI with .NET hosting
3. **Mock Webhook Client** (`mock-webhook-client`) - Testing service

### Integration Testing with Mock Services

#### Mock Webhook Client Integration
```yaml
- name: Start Mock Webhook Client
  run: |
    cd mock-webhook-client
    dotnet MemberPropertyAlert.MockWebhookClient.dll --urls "http://localhost:5000" &
    echo $! > webhook_client.pid
    sleep 10

- name: Run integration tests with mock services
  env:
    MOCK_WEBHOOK_URL: "http://localhost:5000"
  run: |
    # Test webhook functionality
    curl -X POST "$MOCK_WEBHOOK_URL/webhook" \
      -H "Content-Type: application/json" \
      -d '{"test": "webhook", "timestamp": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"}'
    
    # Verify webhook was received
    WEBHOOK_COUNT=$(curl -s "$MOCK_WEBHOOK_URL/webhooks" | jq length)
    if [ "$WEBHOOK_COUNT" -lt 1 ]; then
      echo "ERROR: Mock webhook client did not receive test webhook"
      exit 1
    fi
```

**Testing Capabilities:**
- ✅ Webhook delivery verification
- ✅ Mock service functionality validation
- ✅ End-to-end notification flow testing
- ✅ Real-time webhook monitoring

## Environment Configuration

### Development Environment
```json
{
  "useMockServices": true,
  "mockWebhookUrl": "http://localhost:5000",
  "enableIntegrationTesting": true,
  "logLevel": "Debug"
}
```

### Test Environment
```json
{
  "useMockServices": true,
  "mockWebhookUrl": "https://test-webhook-client.azurewebsites.net",
  "enableIntegrationTesting": true,
  "logLevel": "Information"
}
```

### Production Environment
```json
{
  "useMockServices": false,
  "enableIntegrationTesting": false,
  "logLevel": "Warning"
}
```

## Deployment Strategies

### 1. Blue-Green Deployment with Mock Testing

```yaml
# Deploy to staging slot first
- name: Deploy to Staging
  uses: azure/webapps-deploy@v2
  with:
    app-name: ${{ steps.get-outputs.outputs.web-app-name }}
    slot-name: staging
    package: './web-app'

# Run tests against staging
- name: Test Staging Environment
  run: |
    # Start mock webhook client
    # Run comprehensive tests
    # Validate all functionality

# Swap to production if tests pass
- name: Swap to Production
  run: |
    az webapp deployment slot swap \
      --name ${{ steps.get-outputs.outputs.web-app-name }} \
      --resource-group ${{ env.RESOURCE_GROUP_NAME }} \
      --slot staging \
      --target-slot production
```

### 2. Canary Deployment with Mock Validation

```yaml
# Deploy to canary environment
- name: Deploy Canary
  run: |
    # Deploy 10% traffic to new version
    # Use mock services for validation
    # Monitor metrics and webhook delivery

# Gradually increase traffic
- name: Increase Canary Traffic
  run: |
    # Increase to 50% if metrics are good
    # Continue monitoring with mock services
    # Full rollout if all tests pass
```

## Testing Workflows

### 1. Unit Testing with Mock Services

```bash
# Run unit tests with mock RentCast service
dotnet test --configuration Release \
  --environment:UseMockServices=true \
  --environment:MockRentCast:FailureRate=0.1

# Test notification delivery with mock webhook client
dotnet test --configuration Release \
  --environment:MockWebhookUrl=http://localhost:5000 \
  --environment:NotificationSettings:DeliveryMethods=webhook
```

### 2. Integration Testing Workflow

```bash
# 1. Start all mock services
docker-compose -f docker-compose.test.yml up -d

# 2. Run integration tests
dotnet test IntegrationTests \
  --environment:FunctionAppUrl=$FUNCTION_APP_URL \
  --environment:WebAppUrl=$WEB_APP_URL \
  --environment:MockWebhookUrl=$MOCK_WEBHOOK_URL

# 3. Validate webhook delivery
curl -s "$MOCK_WEBHOOK_URL/webhooks" | jq '.[] | select(.test == true)'

# 4. Clean up
docker-compose -f docker-compose.test.yml down
```

### 3. End-to-End Testing with Real Data

```bash
# Use mock RentCast but real webhook endpoints
export USE_MOCK_RENTCAST=true
export USE_REAL_WEBHOOKS=true

# Run property scan with mock data
curl -X POST "$FUNCTION_APP_URL/api/scan" \
  -H "Content-Type: application/json" \
  -d '{"institutionId": "test-institution", "useMockData": true}'

# Verify notifications were sent
curl -s "$WEBHOOK_ENDPOINT/received-webhooks" | jq length
```

## Monitoring and Observability

### Application Insights Integration

```csharp
// Enhanced telemetry for mock services
services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableAdaptiveSampling = false; // Full sampling for testing
    options.EnableQuickPulseMetricStream = true;
});

// Custom telemetry for mock service usage
services.AddSingleton<ITelemetryInitializer, MockServiceTelemetryInitializer>();
```

### Custom Metrics for Testing

```csharp
public class MockServiceMetrics
{
    private readonly IMetricsLogger _metrics;
    
    public void TrackMockApiCall(string service, bool success, TimeSpan duration)
    {
        _metrics.LogMetric("MockService.ApiCall", 1, new Dictionary<string, object>
        {
            ["Service"] = service,
            ["Success"] = success,
            ["Duration"] = duration.TotalMilliseconds
        });
    }
    
    public void TrackWebhookDelivery(string institutionId, bool success)
    {
        _metrics.LogMetric("MockWebhook.Delivery", 1, new Dictionary<string, object>
        {
            ["InstitutionId"] = institutionId,
            ["Success"] = success
        });
    }
}
```

### Health Check Enhancements

```csharp
// Enhanced health checks for mock services
services.AddHealthChecks()
    .AddCheck<MockRentCastHealthCheck>("mock-rentcast")
    .AddCheck<MockWebhookClientHealthCheck>("mock-webhook-client")
    .AddCheck<NotificationDeliveryHealthCheck>("notification-delivery");
```

## Security Considerations

### Mock Service Security

1. **Environment Isolation**
   ```yaml
   # Ensure mock services only run in non-production environments
   - name: Validate Environment
     run: |
       if [ "$ENVIRONMENT" == "prod" ] && [ "$USE_MOCK_SERVICES" == "true" ]; then
         echo "ERROR: Mock services cannot be enabled in production"
         exit 1
       fi
   ```

2. **API Key Management**
   ```yaml
   # Use different API keys for different environments
   - name: Set API Keys
     run: |
       if [ "$USE_MOCK_SERVICES" == "true" ]; then
         echo "RENTCAST_API_KEY=mock-key-$ENVIRONMENT" >> $GITHUB_ENV
       else
         echo "RENTCAST_API_KEY=${{ secrets.RENTCAST_API_KEY }}" >> $GITHUB_ENV
       fi
   ```

3. **Network Security**
   ```yaml
   # Restrict mock webhook client access
   - name: Configure Network Security
     run: |
       # Only allow localhost connections for mock webhook client
       az webapp config appsettings set \
         --name mock-webhook-client \
         --settings ALLOWED_HOSTS=localhost,127.0.0.1
   ```

## Performance Testing

### Load Testing with Mock Services

```bash
# Load test with mock RentCast service
artillery run load-test-config.yml \
  --environment mock \
  --target $FUNCTION_APP_URL

# Webhook delivery performance test
artillery run webhook-load-test.yml \
  --environment mock \
  --target $MOCK_WEBHOOK_URL
```

### Performance Metrics Collection

```yaml
- name: Collect Performance Metrics
  run: |
    # Measure mock service response times
    curl -w "@curl-format.txt" -s -o /dev/null "$MOCK_WEBHOOK_URL/health"
    
    # Measure end-to-end notification delivery time
    START_TIME=$(date +%s%N)
    curl -X POST "$FUNCTION_APP_URL/api/test-notification"
    END_TIME=$(date +%s%N)
    DURATION=$(( (END_TIME - START_TIME) / 1000000 ))
    echo "Notification delivery time: ${DURATION}ms"
```

## Troubleshooting Guide

### Common CI/CD Issues

#### 1. Node.js Build Failures
```bash
# Clear npm cache
npm cache clean --force

# Verify Node.js version
node --version
npm --version

# Check package-lock.json integrity
npm ci --verbose
```

#### 2. Mock Service Startup Issues
```bash
# Check if port is available
netstat -tulpn | grep :5000

# Verify mock service health
curl -f http://localhost:5000/health || echo "Mock service not responding"

# Check process status
ps aux | grep MemberPropertyAlert.MockWebhookClient
```

#### 3. Integration Test Failures
```bash
# Verify all services are running
curl -f "$FUNCTION_APP_URL/api/health"
curl -f "$WEB_APP_URL/health"
curl -f "$MOCK_WEBHOOK_URL/health"

# Check webhook delivery
curl -s "$MOCK_WEBHOOK_URL/webhooks" | jq '.[] | .timestamp' | tail -5

# Validate environment configuration
curl -s "$FUNCTION_APP_URL/api/config" | jq '.useMockServices'
```

### Debugging Commands

```bash
# View CI/CD logs
gh run view --log

# Check artifact contents
gh run download --name function-app
ls -la function-app/

# Validate deployment
az webapp show --name $WEB_APP_NAME --resource-group $RESOURCE_GROUP_NAME --query state

# Test webhook connectivity
curl -X POST "$MOCK_WEBHOOK_URL/webhook" \
  -H "Content-Type: application/json" \
  -d '{"debug": true, "timestamp": "'$(date -Iseconds)'"}'
```

## Best Practices

### 1. Environment Parity
- Use identical configurations across all environments
- Maintain separate parameter files for each environment
- Use feature flags to control mock service usage

### 2. Testing Strategy
- Run unit tests with mock services in all environments
- Use integration tests to validate webhook delivery
- Implement contract testing between services

### 3. Deployment Safety
- Always test with mock services before production deployment
- Use blue-green deployments for zero-downtime updates
- Implement automatic rollback on test failures

### 4. Monitoring and Alerting
- Monitor mock service usage in non-production environments
- Alert on webhook delivery failures
- Track performance metrics for all components

## Conclusion

The enhanced CI/CD pipeline provides comprehensive support for the new testing and administration features while maintaining production safety and reliability. The integration of mock services enables cost-effective testing and development while the enhanced monitoring and deployment strategies ensure robust production operations.

Key benefits:
- ✅ **Cost-Effective Testing** - No API charges during development and testing
- ✅ **Comprehensive Validation** - End-to-end testing including webhook delivery
- ✅ **Production Safety** - Mock services automatically disabled in production
- ✅ **Enhanced Monitoring** - Detailed metrics and health checks for all components
- ✅ **Flexible Deployment** - Support for various deployment strategies
