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

// Navigation component that uses hooks
const NavigationContent = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const muiTheme = useMuiTheme();
  const { isDarkMode, toggleTheme } = useTheme();
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
  const [connection, setConnection] = useState(null);
  const [logs, setLogs] = useState([]);
  const [isConnected, setIsConnected] = useState(false);
  const muiTheme = useMuiTheme();
  const { isDarkMode, toggleTheme } = useTheme();
  const isMobile = useMediaQuery(muiTheme.breakpoints.down('md'));
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    // Initialize SignalR connection for real-time logs
    const newConnection = new HubConnectionBuilder()
      .withUrl('/api/loghub')
      .build();

    setConnection(newConnection);

    newConnection.start()
      .then(() => {
        console.log('SignalR Connected');
        setIsConnected(true);
        
        // Listen for log messages
        newConnection.on('LogMessage', (logEntry) => {
          setLogs(prevLogs => [...prevLogs, {
            ...logEntry,
            timestamp: new Date().toISOString()
          }]);
        });

        // Listen for scan status updates
        newConnection.on('ScanStatusUpdate', (status) => {
          console.log('Scan status update:', status);
        });
      })
      .catch(err => {
        console.error('SignalR Connection Error:', err);
        setIsConnected(false);
      });

    return () => {
      if (newConnection) {
        newConnection.stop();
      }
    };
  }, []);

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
                <Routes>
                  <Route path="/" element={<Dashboard />} />
                  <Route path="/institutions" element={<InstitutionManager />} />
                </Routes>
              </Box>
              
              {/* Scan Controls */}
              <Paper elevation={2} sx={{ p: 3 }}>
                <ScanControl connection={connection} />
              </Paper>
            </Grid>

            {/* Right Column - Log Viewer */}
            <Grid item xs={12} lg={4}>
              <Paper elevation={2} sx={{ height: 'fit-content' }}>
                <LogViewer 
                  logs={logs} 
                  onClearLogs={clearLogs}
                  isConnected={isConnected}
                />
              </Paper>
            </Grid>
          </Grid>
        </Container>
      </Box>
    </Box>
  );
};

function App() {
  return (
    <CustomThemeProvider>
      <Router>
        <AppContent />
      </Router>
    </CustomThemeProvider>
  );
}

export default App;
