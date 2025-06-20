# Member Property Alert - Admin UI

A modern React-based admin dashboard for managing the Member Property Market Alert service.

## Features

### 🎛️ **Scan Control**
- **Manual Scanning**: Start on-demand property scans with real-time progress
- **Scheduled Scans**: Configure automated daily/weekly scanning
- **Scan Statistics**: Monitor scan performance and results
- **State-Level Monitoring**: Efficient scanning by state instead of individual properties

### 📊 **Dashboard**
- **Real-time Statistics**: Live metrics on institutions, properties, and alerts
- **Activity Feed**: Recent system activity and matches
- **Performance Metrics**: Scan success rates and processing times

### 🏢 **Institution Management**
- **Add/Edit Institutions**: Manage financial institution accounts
- **Property Monitoring**: View properties being monitored per institution
- **Contact Management**: Maintain alert notification contacts

### 📝 **Real-time Log Streaming**
- **Live Activity Logs**: Real-time streaming of system activity
- **Log Filtering**: Filter by log level (Error, Warning, Info, Debug)
- **Log Export**: Download logs for analysis
- **Auto-scroll**: Automatically scroll to latest log entries

## Technology Stack

- **Frontend**: React 18 with modern hooks
- **Styling**: Tailwind CSS for responsive design
- **Real-time**: SignalR for live log streaming
- **HTTP Client**: Axios for API communication
- **Icons**: Lucide React for modern iconography
- **Routing**: React Router for navigation

## Getting Started

### Prerequisites
- Node.js 16+ 
- npm or yarn
- Running Member Property Alert API

### Installation

1. **Install Dependencies**
   ```bash
   cd src/MemberPropertyAlert.UI
   npm install
   ```

2. **Configure API Endpoint**
   The UI is configured to proxy requests to the Azure Functions API:
   ```
   https://func-member-property-alert-dev.azurewebsites.net
   ```

3. **Start Development Server**
   ```bash
   npm start
   ```
   
   The UI will be available at `http://localhost:3000`

### Building for Production

```bash
npm run build
```

This creates an optimized production build in the `build/` folder.

## API Integration

The UI integrates with the Member Property Alert API endpoints:

### Scan Control
- `POST /api/scan/start` - Start manual scan
- `POST /api/scan/stop` - Stop running scan
- `GET /api/scan/schedule` - Get scan schedule
- `PUT /api/scan/schedule` - Update scan schedule
- `GET /api/scan/stats` - Get scan statistics

### Dashboard
- `GET /api/dashboard/stats` - Get dashboard statistics
- `GET /api/dashboard/recent-activity` - Get recent activity

### Institutions
- `GET /api/institutions` - List institutions
- `POST /api/institutions` - Create institution
- `GET /api/institutions/{id}` - Get institution details

### Real-time Updates
- SignalR Hub: `/api/loghub`
- Events: `LogMessage`, `ScanStatusUpdate`, `ScanCompleted`

## Features in Detail

### Scan Control Panel
- **One-click scanning**: Start comprehensive state-level property scans
- **Schedule management**: Set up automated daily scans at specific times
- **Progress monitoring**: Real-time scan progress and statistics
- **Cost optimization**: State-level scanning reduces API costs by 99%+

### Live Log Viewer
- **Real-time streaming**: Logs appear instantly as they're generated
- **Color-coded levels**: Visual distinction between Error, Warning, Info, Debug
- **Downloadable logs**: Export logs for offline analysis
- **Auto-scroll**: Always shows latest activity

### Responsive Design
- **Mobile-friendly**: Works on tablets and mobile devices
- **Modern UI**: Clean, professional interface
- **Accessibility**: Keyboard navigation and screen reader support

## Development

### Project Structure
```
src/
├── components/
│   ├── Dashboard.js          # Main dashboard with statistics
│   ├── ScanControl.js        # Scan management controls
│   ├── LogViewer.js          # Real-time log streaming
│   └── InstitutionManager.js # Institution management
├── App.js                    # Main application component
├── index.js                  # Application entry point
└── index.css                 # Global styles with Tailwind
```

### Adding New Features

1. **Create Component**: Add new React component in `src/components/`
2. **Add Route**: Update `App.js` with new route
3. **API Integration**: Use axios for API calls
4. **Real-time Updates**: Use SignalR connection for live updates

### Styling Guidelines

- Use Tailwind CSS utility classes
- Follow existing color scheme (blue primary, semantic colors)
- Maintain responsive design patterns
- Use Lucide React icons for consistency

## Deployment

The UI can be deployed to any static hosting service:

### Azure Static Web Apps
```bash
npm run build
# Deploy build/ folder to Azure Static Web Apps
```

### Netlify/Vercel
```bash
npm run build
# Deploy build/ folder
```

### Docker
```dockerfile
FROM nginx:alpine
COPY build/ /usr/share/nginx/html/
```

## Monitoring & Troubleshooting

### Connection Issues
- Check SignalR connection status (green/red indicator in header)
- Verify API endpoint accessibility
- Check browser console for errors

### Performance
- Monitor log viewer memory usage with large log volumes
- Consider log retention policies for long-running sessions
- Use browser dev tools to monitor network requests

## Future Enhancements

- **Advanced Filtering**: More sophisticated log filtering options
- **Alerting**: Browser notifications for critical events
- **Analytics**: Historical trend analysis and reporting
- **Multi-tenant**: Support for multiple institution views
- **Mobile App**: Native mobile application for monitoring

## Support

For technical support or feature requests, please refer to the main project documentation.
