import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, useLocation, useNavigate } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Container,
  Grid,
  Paper,
  BottomNavigation,
  BottomNavigationAction,
  IconButton,
  Chip,
  useMediaQuery,
  useTheme as useMuiTheme,
  Drawer,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Divider,
} from '@mui/material';
import {
  Dashboard as DashboardIcon,
  Business as BusinessIcon,
  Brightness4,
  Brightness7,
  Menu as MenuIcon,
  Close as CloseIcon,
} from '@mui/icons-material';
import { CustomThemeProvider, useTheme } from './contexts/ThemeContext';
import Dashboard from './components/Dashboard';
import ScanControl from './components/ScanControl';
import LogViewer from './components/LogViewer';
import InstitutionManager from './components/InstitutionManager';
import ErrorBoundary from './components/ErrorBoundary';
import config from './config/apiConfig';

// Debug logging
// Force deployment trigger - Updated at 2025-01-27 17:17
console.log('🚀 Member Property Alert UI - App.js loading');
console.log('🔧 Environment:', process.env.NODE_ENV);
console.log('🔧 React App API Base URL:', process.env.REACT_APP_API_BASE_URL);
console.log('🔧 React App Environment:', process.env.REACT_APP_ENVIRONMENT);

// Global error handler for unhandled promises
window.addEventListener('unhandledrejection', (event) => {
  console.error('🚨 Unhandled Promise Rejection:', event.reason);
});

// Global error handler for JavaScript errors
window.addEventListener('error', (event) => {
  console.error('🚨 Global JavaScript Error:', event.error);
});

// Navigation component that uses hooks
const NavigationContent = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const muiTheme = useMuiTheme();
  const isMobile = useMediaQuery(muiTheme.breakpoints.down('md'));
  const [mobileOpen, setMobileOpen] = useState(false);

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  const navigationItems = [
    { label: 'Dashboard', path: '/', icon: <DashboardIcon /> },
    { label: 'Institutions', path: '/institutions', icon: <BusinessIcon /> },
  ];

  const drawer = (
    <Box sx={{ width: 250 }}>
      <Box sx={{ p: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6" component="div">
          Menu
        </Typography>
        <IconButton onClick={handleDrawerToggle}>
          <CloseIcon />
        </IconButton>
      </Box>
      <Divider />
      <List>
        {navigationItems.map((item) => (
          <ListItem
            button
            key={item.path}
            selected={location.pathname === item.path}
            onClick={() => {
              navigate(item.path);
              setMobileOpen(false);
            }}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItem>
        ))}
      </List>
    </Box>
  );

  return (
    <>
      {/* Mobile Drawer */}
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={handleDrawerToggle}
        ModalProps={{
          keepMounted: true, // Better open performance on mobile.
        }}
        sx={{
          display: { xs: 'block', md: 'none' },
          '& .MuiDrawer-paper': { boxSizing: 'border-box', width: 250 },
        }}
      >
        {drawer}
      </Drawer>

      {/* Bottom Navigation for Mobile */}
      {isMobile && (
        <Paper
          sx={{ position: 'fixed', bottom: 0, left: 0, right: 0, zIndex: 1000 }}
          elevation={3}
        >
          <BottomNavigation
            value={location.pathname}
            onChange={(event, newValue) => {
              navigate(newValue);
            }}
            showLabels
          >
            {navigationItems.map((item) => (
              <BottomNavigationAction
                key={item.path}
                label={item.label}
                value={item.path}
                icon={item.icon}
              />
            ))}
          </BottomNavigation>
        </Paper>
      )}
    </>
  );
};

// Connection status component
const ConnectionStatus = ({ isConnected }) => (
  <Chip
    icon={
      <Box
        sx={{
          width: 8,
          height: 8,
          borderRadius: '50%',
          backgroundColor: isConnected ? 'success.main' : 'error.main',
        }}
      />
    }
    label={isConnected ? 'Connected' : 'Disconnected'}
    color={isConnected ? 'success' : 'error'}
    variant="outlined"
    size="small"
  />
);

// Main App Content
const AppContent = () => {
  console.log('🚀 AppContent component rendering...');
  
  const [connection, setConnection] = useState(null); // SignalR connection
  const [logs, setLogs] = useState([]);
  const [isConnected, setIsConnected] = useState(false);
  const muiTheme = useMuiTheme();
  const { isDarkMode, toggleTheme } = useTheme();
  const isMobile = useMediaQuery(muiTheme.breakpoints.down('md'));
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    // SignalR connection - connect to the Function App's LogHub
    console.log('🔌 Setting up SignalR connection to Function App LogHub...');
    
    let connectionInstance = null;
    
    try {
      // Connect to the Function App's SignalR hub
      const hubUrl = `${config.apiBaseUrl}/loghub`;
      console.log('🔗 SignalR Hub URL:', hubUrl);
      
      // Initialize SignalR connection for real-time logs
      connectionInstance = new HubConnectionBuilder()
        .withUrl(hubUrl)
        .build();

      setConnection(connectionInstance);

      connectionInstance.start()
        .then(() => {
          console.log('✅ SignalR Connected to Function App LogHub');
          setIsConnected(true);
          
          // Listen for log messages
          connectionInstance.on('LogMessage', (logEntry) => {
            console.log('📨 Received log message:', logEntry);
            setLogs(prevLogs => [...prevLogs, {
              ...logEntry,
              timestamp: logEntry.Timestamp || new Date().toISOString()
            }]);
          });

          // Listen for scan status updates
          connectionInstance.on('ScanStatusUpdate', (status) => {
            console.log('📊 Scan status update:', status);
            // Add scan status as a log entry too
            setLogs(prevLogs => [...prevLogs, {
              Level: 'Info',
              Message: `Scan Update: ${status.Status} - ${status.Data?.Message || 'Status changed'}`,
              Source: 'ScanMonitor',
              timestamp: new Date().toISOString()
            }]);
          });

          // Send a test message to verify connection
          setTimeout(() => {
            fetch(`${config.apiBaseUrl}/loghub/test`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' }
            }).then(() => {
              console.log('🧪 Test message sent to SignalR hub');
            }).catch(err => {
              console.warn('⚠️ Could not send test message:', err);
            });
          }, 2000);
        })
        .catch(err => {
          console.error('❌ SignalR Connection Error:', err);
          setIsConnected(false);
        });
    } catch (error) {
      console.error('❌ Error setting up SignalR:', error);
      setIsConnected(false);
    }

    return () => {
      console.log('🔌 Cleaning up SignalR connection...');
      if (connectionInstance) {
        connectionInstance.stop().catch(err => console.error('Error stopping SignalR:', err));
      }
    };
  }, []); // Empty dependency array - only run once on mount

  const clearLogs = () => {
    setLogs([]);
  };

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      {/* App Bar */}
      <AppBar position="sticky" elevation={2}>
        <Toolbar>
          {isMobile && (
            <IconButton
              color="inherit"
              aria-label="open drawer"
              edge="start"
              onClick={handleDrawerToggle}
              sx={{ mr: 2 }}
            >
              <MenuIcon />
            </IconButton>
          )}
          
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            Member Property Alert - Admin Dashboard
          </Typography>
          
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <ConnectionStatus isConnected={isConnected} />
            
            <IconButton
              color="inherit"
              onClick={toggleTheme}
              aria-label="toggle theme"
            >
              {isDarkMode ? <Brightness7 /> : <Brightness4 />}
            </IconButton>
          </Box>
        </Toolbar>
      </AppBar>

      {/* Navigation */}
      <NavigationContent />

      {/* Main Content */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          bgcolor: 'background.default',
          pb: isMobile ? 8 : 3, // Add bottom padding for mobile navigation
        }}
      >
        <Container maxWidth="xl" sx={{ py: 3 }}>
          <Grid container spacing={3}>
            {/* Left Column - Main Content */}
            <Grid item xs={12} lg={8}>
              <Box sx={{ mb: 3 }}>
                <ErrorBoundary>
                  <Routes>
                    <Route path="/" element={<Dashboard />} />
                    <Route path="/institutions" element={<InstitutionManager />} />
                  </Routes>
                </ErrorBoundary>
              </Box>
              
              {/* Scan Controls */}
              <Paper elevation={2} sx={{ p: 3 }}>
                <ErrorBoundary>
                  <ScanControl connection={connection} />
                </ErrorBoundary>
              </Paper>
            </Grid>

            {/* Right Column - Log Viewer */}
            <Grid item xs={12} lg={4}>
              <Paper elevation={2} sx={{ height: 'fit-content' }}>
                <ErrorBoundary>
                  <LogViewer 
                    logs={logs} 
                    onClearLogs={clearLogs}
                    isConnected={isConnected}
                  />
                </ErrorBoundary>
              </Paper>
            </Grid>
          </Grid>
        </Container>
      </Box>
    </Box>
  );
};

function App() {
  console.log('🚀 App component rendering...');
  
  return (
    <ErrorBoundary>
      <CustomThemeProvider>
        <Router>
          <ErrorBoundary>
            <AppContent />
          </ErrorBoundary>
        </Router>
      </CustomThemeProvider>
    </ErrorBoundary>
  );
}

export default App;
