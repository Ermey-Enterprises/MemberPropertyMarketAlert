# Member Property Market Alert - Web UI Guide

## Overview

The Web UI provides a user-friendly interface for testing and monitoring the Member Property Market Alert service. It includes a dashboard for observability and an address management interface for testing the API.

## Features

### 1. Dashboard (Home Page)
- **Real-time Metrics**: Display key statistics like total addresses, active matches, pending reviews, and institutions
- **Activity Charts**: Visual representation of recent activity using Chart.js
- **Recent Alerts**: Timeline of recent property matches and system events
- **Quick Actions**: Easy access to common tasks

### 2. Address Management
- **Single Address Creation**: Form-based interface for adding individual member addresses
- **Bulk Upload**: CSV-based bulk address upload with validation
- **Address Search**: Search and view addresses by institution ID
- **Address Management**: View, edit, and delete existing addresses

## Getting Started

### Prerequisites
- .NET 8 SDK
- Azure Functions running locally (see Local Development Guide)
- Modern web browser

### Running the Web UI

1. **Start the Azure Functions API** (in a separate terminal):
   ```bash
   cd src/MemberPropertyMarketAlert.Functions
   func start
   ```

2. **Start the Web UI**:
   ```bash
   cd src/MemberPropertyMarketAlert.Web
   dotnet run
   ```

3. **Access the application**:
   - Open your browser to: `https://localhost:5001` or `http://localhost:5000`

## Using the Web Interface

### Dashboard Overview

The dashboard provides a comprehensive view of the system status:

- **Metrics Cards**: Show current counts for addresses, matches, reviews, and institutions
- **Activity Chart**: Line chart showing trends for new matches and addresses over the past week
- **Recent Alerts**: Real-time feed of system events and matches
- **Quick Actions**: Direct links to common tasks

### Managing Addresses

#### Adding a Single Address

1. Navigate to **Address Management**
2. Fill out the **Add Single Address** form:
   - **Institution ID**: Unique identifier for the financial institution
   - **Anonymous Reference ID**: Anonymous identifier for the member
   - **Address**: Street address
   - **City**: City name
   - **State**: Two-letter state code
   - **ZIP Code**: Postal code
3. Click **Add Address**

#### Bulk Upload

1. Navigate to **Address Management**
2. In the **Bulk Upload** section:
   - Enter the **Institution ID**
   - Paste CSV data in the format:
     ```
     anonymousReferenceId,address,city,state,zipCode
     member-001,123 Main St,Springfield,IL,62701
     member-002,456 Oak Ave,Decatur,IL,62521
     ```
3. Click **Upload Addresses**

#### Viewing Existing Addresses

1. Navigate to **Address Management**
2. In the **Existing Addresses** section:
   - Enter an **Institution ID** in the search box
   - Click **Search**
3. View the results in the table below
4. Use the delete button (trash icon) to remove addresses

## API Integration

The Web UI communicates with the Azure Functions API through the `ApiClientService`. The base URL is configured in `appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:7071/api"
  }
}
```

### API Endpoints Used

- `POST /api/members/addresses` - Create single address
- `POST /api/members/addresses/bulk` - Bulk create addresses
- `GET /api/members/addresses/{institutionId}` - Get addresses by institution
- `DELETE /api/members/addresses/{id}` - Delete address

## Testing Scenarios

### Scenario 1: Basic Address Management

1. **Add a test institution**:
   - Institution ID: `test-bank-001`
   - Add several addresses for testing

2. **Test bulk upload**:
   ```
   member-001,123 Main St,Springfield,IL,62701
   member-002,456 Oak Ave,Springfield,IL,62702
   member-003,789 Pine Rd,Decatur,IL,62521
   ```

3. **Search and verify**:
   - Search for `test-bank-001`
   - Verify all addresses appear
   - Test delete functionality

### Scenario 2: Multiple Institutions

1. **Create multiple test institutions**:
   - `credit-union-001`
   - `community-bank-002`
   - `savings-loan-003`

2. **Add addresses to each**
3. **Verify isolation**: Ensure addresses only appear for their respective institutions

### Scenario 3: Error Handling

1. **Test validation**:
   - Try submitting forms with missing fields
   - Test invalid state codes
   - Test malformed CSV data

2. **Test API errors**:
   - Stop the Azure Functions API
   - Try to perform operations
   - Verify error messages display correctly

## Customization

### Styling

The Web UI uses Bootstrap 5 for styling. Custom styles can be added to `wwwroot/css/site.css`.

### Adding New Features

1. **New Controller Actions**: Add methods to `HomeController.cs`
2. **New Views**: Create Razor views in `Views/Home/`
3. **API Integration**: Extend `ApiClientService.cs` for new endpoints
4. **Client-side Logic**: Add JavaScript to view files or `wwwroot/js/site.js`

### Configuration

Update `appsettings.json` to modify:
- API base URL
- Logging levels
- Other application settings

## Monitoring and Observability

### Dashboard Metrics

The dashboard displays simulated metrics. In a production environment, these would be populated from:
- Azure Application Insights
- Cosmos DB queries
- Azure Functions metrics

### Real-time Updates

For production use, consider implementing:
- SignalR for real-time dashboard updates
- WebSocket connections for live alerts
- Automatic refresh of metrics

### Logging

The Web UI logs to the console and can be configured to log to:
- Azure Application Insights
- File system
- Other logging providers

## Deployment

### Local Development
- Run with `dotnet run`
- Uses development settings from `appsettings.Development.json`

### Production Deployment
- Deploy to Azure App Service
- Configure production API URLs
- Set up Application Insights
- Configure authentication if required

## Security Considerations

### Current Implementation
- No authentication (suitable for development/testing)
- Direct API calls to Azure Functions
- Client-side validation only

### Production Recommendations
- Implement Azure AD authentication
- Add server-side validation
- Use HTTPS only
- Implement rate limiting
- Add CSRF protection

## Troubleshooting

### Common Issues

1. **API Connection Errors**:
   - Verify Azure Functions are running on port 7071
   - Check `appsettings.json` API base URL
   - Ensure CORS is configured if needed

2. **Form Submission Errors**:
   - Check browser developer tools for JavaScript errors
   - Verify form field names match controller action parameters
   - Check server logs for validation errors

3. **Data Not Loading**:
   - Verify institution IDs are correct
   - Check that addresses exist in the database
   - Ensure Cosmos DB emulator is running

### Debug Mode

Run in debug mode to get detailed error information:
```bash
dotnet run --environment Development
```

## Future Enhancements

Potential improvements for the Web UI:

1. **Enhanced Dashboard**:
   - Real-time metrics from Application Insights
   - Interactive charts with drill-down capabilities
   - Configurable dashboard widgets

2. **Advanced Address Management**:
   - File upload for CSV files
   - Address validation and geocoding
   - Bulk edit capabilities
   - Import/export functionality

3. **Property Match Visualization**:
   - Map-based visualization of matches
   - Match confidence indicators
   - Detailed match reports

4. **User Management**:
   - Role-based access control
   - Institution-specific views
   - Audit logging

5. **Reporting**:
   - Downloadable reports
   - Scheduled report generation
   - Custom report builder

6. **Notifications**:
   - Email alerts for new matches
   - In-app notifications
   - Webhook configuration UI
