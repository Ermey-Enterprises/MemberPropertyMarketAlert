# Testing and Administration Enhancements

This document outlines the significant enhancements made to the Member Property Alert application for testing and administration purposes.

## Overview

The following enhancements have been implemented to improve testing capabilities and provide comprehensive administration features:

1. **Mock RentCast API Service** - For testing without API costs
2. **Enhanced Institution Management** - Comprehensive per-institution settings
3. **Multiple Notification Delivery Options** - Webhook, CSV, and Email notifications
4. **Mock Webhook Client Service** - For demonstration and testing of webhook notifications
5. **Per-Institution Configuration** - Granular control over institution-specific settings

## 1. Mock RentCast API Service

### Purpose
Provides a realistic simulation of the RentCast API for testing purposes, eliminating API usage costs during development and testing phases.

### Location
- **File**: `src/MemberPropertyAlert.Functions/Services/MockRentCastService.cs`
- **Interface**: Implements `IRentCastService`

### Features
- **Realistic Data Generation**: Creates consistent mock property data with realistic addresses, prices, and property details
- **Configurable Failure Simulation**: Simulates API failures with configurable failure rates
- **Delayed Responses**: Mimics real API response times with configurable delays
- **State-Specific Data**: Generates location-appropriate property data for different states
- **Consistent Results**: Returns the same data for the same address queries

### Configuration
```json
{
  "MockRentCast": {
    "SimulatedDelayMs": 500,
    "FailureRate": 0.05,
    "EnableRandomData": true,
    "MaxPropertiesPerRequest": 100
  }
}
```

### Usage
The mock service can be enabled per institution through the `UseMockServices` configuration setting, allowing for mixed testing scenarios.

## 2. Enhanced Institution Management

### Enhanced Institution Model
The Institution model has been significantly expanded to support comprehensive configuration:

#### New Properties
- **NotificationSettings**: Complete notification delivery configuration
- **Configuration**: Institution-specific operational settings

#### Notification Settings Structure
```csharp
public class NotificationSettings
{
    public List<NotificationDeliveryMethod> DeliveryMethods { get; set; }
    public WebhookSettings? WebhookSettings { get; set; }
    public EmailSettings? EmailSettings { get; set; }
    public CsvSettings? CsvSettings { get; set; }
    public bool EnableBatching { get; set; }
    public int BatchSize { get; set; }
    public int BatchTimeoutMinutes { get; set; }
}
```

#### Institution Configuration Structure
```csharp
public class InstitutionConfiguration
{
    public bool UseMockServices { get; set; }
    public string? RentCastApiKey { get; set; }
    public ScanConfiguration ScanSettings { get; set; }
    public AlertConfiguration AlertSettings { get; set; }
    public Dictionary<string, object> CustomSettings { get; set; }
}
```

### Enhanced UI Components
The Institution Manager UI has been updated to support:
- **Settings Modal**: Comprehensive configuration interface
- **Notification Method Selection**: Choose between webhook, email, and CSV delivery
- **Per-Institution API Configuration**: Individual RentCast API keys and mock service toggles
- **Advanced Alert Filtering**: Price ranges, property types, and market timing filters

## 3. Multiple Notification Delivery Options

### Supported Delivery Methods

#### 1. Webhook Notifications
- **Enhanced Configuration**: Custom headers, authentication, retry policies
- **SSL Verification**: Configurable SSL certificate validation
- **Timeout Management**: Configurable request timeouts
- **Retry Logic**: Exponential backoff with configurable retry attempts

#### 2. Email Notifications
- **Multiple Recipients**: Support for multiple email addresses
- **Format Options**: HTML or plain text formatting
- **Custom Templates**: Configurable email templates
- **Attachment Support**: Optional file attachments

#### 3. CSV Notifications
- **Flexible Delivery**: Email, webhook, or FTP delivery options
- **Customizable Format**: Configurable delimiters and date formats
- **Field Selection**: Choose which property fields to include
- **Header Options**: Optional column headers

### Enhanced Notification Service Interface
```csharp
public interface INotificationService
{
    // Multi-method delivery
    Task<List<NotificationResult>> SendNotificationAsync(PropertyAlert alert, Institution institution);
    Task<List<NotificationResult>> SendBulkNotificationAsync(List<PropertyAlert> alerts, Institution institution);
    
    // Method-specific delivery
    Task<NotificationResult> SendWebhookAsync(PropertyAlert alert, Institution institution);
    Task<NotificationResult> SendEmailAsync(PropertyAlert alert, Institution institution);
    Task<NotificationResult> SendCsvAsync(List<PropertyAlert> alerts, Institution institution);
}
```

## 4. Mock Webhook Client Service

### Purpose
Provides a demonstration service that can receive and display webhook notifications for testing and demonstration purposes.

### Location
- **Project**: `src/MemberPropertyAlert.MockWebhookClient/`
- **Main File**: `Program.cs`
- **Project File**: `MemberPropertyAlert.MockWebhookClient.csproj`

### Features
- **Real-time Webhook Reception**: Receives and stores webhook notifications
- **Web-based Viewer**: Interactive web interface for viewing received webhooks
- **Auto-refresh**: Automatic updates of webhook list
- **Detailed Inspection**: View headers, body, and metadata for each webhook
- **Statistics Dashboard**: Shows total webhooks and recent activity
- **Clear Functionality**: Ability to clear webhook history

### Web Interface Features
- **Responsive Design**: Works on desktop and mobile devices
- **Real-time Updates**: Auto-refreshes every 5 seconds
- **Expandable Details**: Click to view full webhook details
- **JSON Formatting**: Automatic formatting of JSON payloads
- **Search and Filter**: Easy navigation through webhook history

### Running the Mock Webhook Client
```bash
cd src/MemberPropertyAlert.MockWebhookClient
dotnet run
```

The service will start on `http://localhost:5000` by default and provide:
- **Webhook Endpoint**: `http://localhost:5000/webhook`
- **Web Interface**: `http://localhost:5000/`
- **API Endpoints**: 
  - `GET /webhooks` - List all received webhooks
  - `GET /webhooks/{id}` - Get specific webhook
  - `DELETE /webhooks` - Clear all webhooks
  - `GET /health` - Health check

## 5. Per-Institution Configuration

### Scan Settings
Each institution can configure:
- **Concurrency Limits**: Maximum concurrent property scans
- **Rate Limiting**: Delay between API requests
- **Timeout Settings**: Request timeout values
- **Caching Configuration**: Enable/disable caching and duration
- **State Exclusions**: Exclude specific states from scanning

### Alert Settings
Institutions can set:
- **Price Ranges**: Minimum and maximum property prices
- **Property Specifications**: Bedroom and bathroom counts
- **Property Types**: Single family, condo, townhouse, etc.
- **Market Timing**: Maximum days on market, new listings only
- **Custom Filters**: Additional filtering criteria

### Service Configuration
- **Mock Service Toggle**: Enable/disable mock services per institution
- **API Key Management**: Individual RentCast API keys
- **Custom Settings**: Extensible configuration for future enhancements

## Implementation Benefits

### For Testing
1. **Cost Reduction**: No API charges during testing with mock services
2. **Predictable Data**: Consistent test data for reliable testing
3. **Failure Simulation**: Test error handling and retry logic
4. **Webhook Testing**: Complete webhook flow testing with mock client

### For Administration
1. **Granular Control**: Per-institution configuration and settings
2. **Flexible Notifications**: Multiple delivery methods per institution
3. **Easy Management**: Comprehensive UI for all settings
4. **Monitoring**: Real-time webhook monitoring and debugging

### For Production
1. **Scalability**: Institution-specific settings support growth
2. **Reliability**: Enhanced retry logic and error handling
3. **Flexibility**: Multiple notification methods for different needs
4. **Maintainability**: Clear separation of concerns and configuration

## Configuration Examples

### Institution with Webhook Notifications
```json
{
  "name": "First National Credit Union",
  "contactEmail": "alerts@firstnational.com",
  "notificationSettings": {
    "deliveryMethods": ["webhook"],
    "webhookSettings": {
      "url": "https://firstnational.com/api/property-alerts",
      "authHeader": "Bearer token123",
      "timeoutSeconds": 30,
      "retryPolicy": {
        "maxRetries": 3,
        "backoffSeconds": [30, 60, 120]
      }
    }
  },
  "configuration": {
    "useMockServices": false,
    "rentCastApiKey": "real-api-key",
    "alertSettings": {
      "minPrice": 100000,
      "maxPrice": 500000,
      "propertyTypes": ["Single Family", "Townhouse"]
    }
  }
}
```

### Institution with Email and CSV Notifications
```json
{
  "name": "Community Bank",
  "contactEmail": "admin@communitybank.com",
  "notificationSettings": {
    "deliveryMethods": ["email", "csv"],
    "emailSettings": {
      "recipients": ["alerts@communitybank.com", "manager@communitybank.com"],
      "subject": "Daily Property Alert Report",
      "format": "Html"
    },
    "csvSettings": {
      "deliveryMethod": "email",
      "includeHeaders": true,
      "includeFields": ["address", "price", "bedrooms", "bathrooms", "listingDate"]
    }
  },
  "configuration": {
    "useMockServices": true,
    "alertSettings": {
      "onlyNewListings": true,
      "maxDaysOnMarket": 7
    }
  }
}
```

## Future Enhancements

### Planned Features
1. **Advanced Analytics**: Institution-specific reporting and analytics
2. **Custom Notification Templates**: Fully customizable notification formats
3. **Integration APIs**: RESTful APIs for third-party integrations
4. **Audit Logging**: Comprehensive audit trails for all actions
5. **Role-Based Access**: Different permission levels for institution users

### Extensibility Points
1. **Custom Notification Channels**: Plugin architecture for new delivery methods
2. **Custom Data Sources**: Support for additional property data providers
3. **Custom Filters**: Extensible filtering system for complex criteria
4. **Custom Workflows**: Configurable business logic workflows

## Conclusion

These enhancements significantly improve the testing capabilities and administrative functionality of the Member Property Alert application. The mock services enable cost-effective testing, while the enhanced institution management provides the flexibility needed for a multi-tenant SaaS application. The multiple notification delivery options ensure that institutions can receive alerts in their preferred format, and the comprehensive configuration system allows for fine-tuned control over all aspects of the service.
