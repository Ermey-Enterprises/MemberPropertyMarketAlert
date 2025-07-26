import React, { useState, useEffect } from 'react';
import {
  Box,
  Typography,
  Grid,
  Card,
  CardContent,
  Avatar,
  List,
  ListItem,
  ListItemAvatar,
  ListItemText,
  Divider,
  Chip,
  useTheme,
  alpha,
  Alert,
  Button,
  Collapse,
} from '@mui/material';
import {
  Business as BusinessIcon,
  Home as HomeIcon,
  Warning as WarningIcon,
  TrendingUp as TrendingUpIcon,
  Circle as CircleIcon,
} from '@mui/icons-material';
import axios from 'axios';
import config from '../config/apiConfig';
import { debugApiConnectivity, testUrlPatterns } from '../utils/apiDebugger';

const Dashboard = () => {
  console.log('🚀 Dashboard component rendering...');
  
  const theme = useTheme();
  const [stats, setStats] = useState({
    totalInstitutions: 0,
    totalProperties: 0,
    activeAlerts: 0,
    recentMatches: 0
  });
  const [recentActivity, setRecentActivity] = useState([]);
  const [error, setError] = useState(null);
  const [debugResults, setDebugResults] = useState(null);
  const [showDebug, setShowDebug] = useState(false);
  const [isDebugging, setIsDebugging] = useState(false);

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    try {
      console.log('📊 Loading dashboard data...');
      console.log('📊 API Base URL:', config.apiBaseUrl);
      setError(null);
      
      const statsUrl = `${config.apiBaseUrl}/dashboard/stats`;
      const activityUrl = `${config.apiBaseUrl}/dashboard/recent-activity`;
      
      console.log('📊 Stats URL:', statsUrl);
      console.log('📊 Activity URL:', activityUrl);
      
      // Load stats and activity separately to better identify which one fails
      let statsData = null;
      let activityData = [];
      
      try {
        console.log('📊 Fetching stats...');
        const statsResponse = await axios.get(statsUrl, {
          timeout: 10000,
          headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
          }
        });
        statsData = statsResponse.data;
        console.log('✅ Dashboard stats loaded:', statsData);
      } catch (statsError) {
        console.error('❌ Failed to load stats:', statsError);
        console.error('❌ Stats error details:', {
          message: statsError.message,
          status: statsError.response?.status,
          statusText: statsError.response?.statusText,
          url: statsUrl
        });
        // Use default stats if API fails
        statsData = {
          totalInstitutions: 0,
          totalProperties: 0,
          activeAlerts: 0,
          recentMatches: 0
        };
      }
      
      try {
        console.log('📊 Fetching recent activity...');
        const activityResponse = await axios.get(activityUrl, {
          timeout: 10000,
          headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
          }
        });
        activityData = activityResponse.data;
        console.log('✅ Dashboard activity loaded:', activityData);
      } catch (activityError) {
        console.error('❌ Failed to load recent activity:', activityError);
        console.error('❌ Activity error details:', {
          message: activityError.message,
          status: activityError.response?.status,
          statusText: activityError.response?.statusText,
          url: activityUrl
        });
        // Use empty array if API fails
        activityData = [];
      }
      
      setStats(statsData);
      setRecentActivity(activityData);
      
    } catch (error) {
      console.error('❌ Unexpected error in loadDashboardData:', error);
      setError(`Failed to load dashboard data: ${error.message}`);
    }
  };

  const statCards = [
    {
      title: 'Institutions',
      value: stats.totalInstitutions || 0,
      icon: BusinessIcon,
      color: theme.palette.primary.main,
      bgColor: alpha(theme.palette.primary.main, 0.1),
    },
    {
      title: 'Properties Monitored',
      value: (stats.totalProperties || 0).toLocaleString(),
      icon: HomeIcon,
      color: theme.palette.success.main,
      bgColor: alpha(theme.palette.success.main, 0.1),
    },
    {
      title: 'Active Alerts',
      value: stats.activeAlerts || 0,
      icon: WarningIcon,
      color: theme.palette.warning.main,
      bgColor: alpha(theme.palette.warning.main, 0.1),
    },
    {
      title: 'Recent Matches',
      value: stats.recentMatches || 0,
      icon: TrendingUpIcon,
      color: theme.palette.error.main,
      bgColor: alpha(theme.palette.error.main, 0.1),
    },
  ];

  const getActivityColor = (type) => {
    switch (type) {
      case 'match':
        return theme.palette.error.main;
      case 'scan':
        return theme.palette.primary.main;
      default:
        return theme.palette.grey[500];
    }
  };

  const getActivityChipColor = (type) => {
    switch (type) {
      case 'match':
        return 'error';
      case 'scan':
        return 'primary';
      default:
        return 'default';
    }
  };

  const handleDebugApi = async () => {
    setIsDebugging(true);
    try {
      console.log('🔍 Running API debug tests...');
      testUrlPatterns(); // Log URL pattern tests
      const results = await debugApiConnectivity();
      setDebugResults(results);
      setShowDebug(true);
    } catch (error) {
      console.error('❌ Debug test failed:', error);
      setDebugResults({
        baseUrl: config.apiBaseUrl,
        error: error.message,
        tests: []
      });
      setShowDebug(true);
    } finally {
      setIsDebugging(false);
    }
  };

  return (
    <Box>
      {/* Error Alert */}
      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}
      
      {/* Header */}
      <Box sx={{ mb: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom fontWeight="bold">
          Dashboard
        </Typography>
        <Typography variant="subtitle1" color="text.secondary">
          Monitor your property alert system
        </Typography>
      </Box>

      {/* Stats Grid */}
      <Grid container spacing={3} sx={{ mb: 4 }}>
        {statCards.map((stat, index) => {
          const IconComponent = stat.icon;
          return (
            <Grid item xs={12} sm={6} lg={3} key={index}>
              <Card
                elevation={2}
                sx={{
                  height: '100%',
                  transition: 'all 0.3s ease-in-out',
                  '&:hover': {
                    elevation: 4,
                    transform: 'translateY(-2px)',
                  },
                }}
              >
                <CardContent sx={{ p: 3 }}>
                  <Box sx={{ display: 'flex', alignItems: 'center' }}>
                    <Avatar
                      sx={{
                        bgcolor: stat.bgColor,
                        color: stat.color,
                        width: 56,
                        height: 56,
                        mr: 2,
                      }}
                    >
                      <IconComponent fontSize="large" />
                    </Avatar>
                    <Box sx={{ flexGrow: 1 }}>
                      <Typography
                        variant="body2"
                        color="text.secondary"
                        fontWeight="medium"
                        gutterBottom
                      >
                        {stat.title}
                      </Typography>
                      <Typography
                        variant="h4"
                        component="div"
                        fontWeight="bold"
                        color="text.primary"
                      >
                        {stat.value}
                      </Typography>
                    </Box>
                  </Box>
                </CardContent>
              </Card>
            </Grid>
          );
        })}
      </Grid>

      {/* Recent Activity */}
      <Card elevation={2}>
        <CardContent sx={{ p: 0 }}>
          <Box sx={{ p: 3, pb: 1 }}>
            <Typography variant="h6" component="h2" fontWeight="bold">
              Recent Activity
            </Typography>
          </Box>
          
          {recentActivity.length === 0 ? (
            <Box sx={{ p: 4, textAlign: 'center' }}>
              <Typography variant="body1" color="text.secondary">
                No recent activity
              </Typography>
            </Box>
          ) : (
            <List sx={{ pt: 0 }}>
              {recentActivity.map((activity, index) => (
                <React.Fragment key={index}>
                  <ListItem
                    sx={{
                      py: 2,
                      px: 3,
                      '&:hover': {
                        bgcolor: alpha(theme.palette.primary.main, 0.04),
                      },
                    }}
                  >
                    <ListItemAvatar>
                      <Avatar
                        sx={{
                          bgcolor: alpha(getActivityColor(activity.type), 0.1),
                          color: getActivityColor(activity.type),
                          width: 40,
                          height: 40,
                        }}
                      >
                        <CircleIcon fontSize="small" />
                      </Avatar>
                    </ListItemAvatar>
                    <ListItemText
                      primary={
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                          <Typography variant="body1" component="span">
                            {activity.message}
                          </Typography>
                          <Chip
                            label={activity.type}
                            size="small"
                            color={getActivityChipColor(activity.type)}
                            variant="outlined"
                          />
                        </Box>
                      }
                      secondary={
                        <Typography variant="caption" color="text.secondary">
                          {new Date(activity.timestamp).toLocaleString()}
                        </Typography>
                      }
                    />
                  </ListItem>
                  {index < recentActivity.length - 1 && (
                    <Divider variant="inset" component="li" />
                  )}
                </React.Fragment>
              ))}
            </List>
          )}
        </CardContent>
      </Card>

      {/* Debug Section */}
      <Box sx={{ mt: 4 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
          <Button
            variant="outlined"
            onClick={handleDebugApi}
            disabled={isDebugging}
            size="small"
          >
            {isDebugging ? 'Running Debug Tests...' : 'Debug API Connectivity'}
          </Button>
          {debugResults && (
            <Button
              variant="text"
              onClick={() => setShowDebug(!showDebug)}
              size="small"
            >
              {showDebug ? 'Hide Debug Results' : 'Show Debug Results'}
            </Button>
          )}
        </Box>

        <Collapse in={showDebug}>
          {debugResults && (
            <Card elevation={1} sx={{ bgcolor: alpha(theme.palette.info.main, 0.05) }}>
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  API Debug Results
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Base URL: {debugResults.baseUrl}
                </Typography>
                
                {debugResults.error && (
                  <Alert severity="error" sx={{ mb: 2 }}>
                    Debug Error: {debugResults.error}
                  </Alert>
                )}

                <List dense>
                  {debugResults.tests.map((test, index) => (
                    <ListItem key={index} sx={{ px: 0 }}>
                      <ListItemText
                        primary={
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <Typography variant="body2" fontWeight="medium">
                              {test.name}
                            </Typography>
                            <Chip
                              label={test.ok ? 'SUCCESS' : test.error ? 'ERROR' : `${test.status}`}
                              size="small"
                              color={test.ok ? 'success' : 'error'}
                              variant="outlined"
                            />
                            {test.responseTime && (
                              <Chip
                                label={`${test.responseTime}ms`}
                                size="small"
                                variant="outlined"
                              />
                            )}
                          </Box>
                        }
                        secondary={
                          <Box sx={{ mt: 1 }}>
                            <Typography variant="caption" color="text.secondary">
                              URL: {test.url}
                            </Typography>
                            {test.error && (
                              <Typography variant="caption" color="error.main" display="block">
                                Error: {test.error}
                              </Typography>
                            )}
                            {test.statusText && !test.ok && (
                              <Typography variant="caption" color="error.main" display="block">
                                Status: {test.status} {test.statusText}
                              </Typography>
                            )}
                          </Box>
                        }
                      />
                    </ListItem>
                  ))}
                </List>
              </CardContent>
            </Card>
          )}
        </Collapse>
      </Box>
    </Box>
  );
};

export default Dashboard;
