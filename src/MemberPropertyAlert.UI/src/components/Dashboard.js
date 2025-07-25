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

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    try {
      console.log('📊 Loading dashboard data...');
      setError(null);
      
      const [statsResponse, activityResponse] = await Promise.all([
        axios.get(`${config.apiBaseUrl}/dashboard/stats`),
        axios.get(`${config.apiBaseUrl}/dashboard/recent-activity`)
      ]);
      
      console.log('✅ Dashboard stats loaded:', statsResponse.data);
      console.log('✅ Dashboard activity loaded:', activityResponse.data);
      
      setStats(statsResponse.data);
      setRecentActivity(activityResponse.data);
    } catch (error) {
      console.error('❌ Failed to load dashboard data:', error);
      setError(error.message || 'Failed to load dashboard data');
      // Don't throw, just log and set error state
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
    </Box>
  );
};

export default Dashboard;
