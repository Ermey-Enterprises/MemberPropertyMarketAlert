import React, { useRef, useEffect } from 'react';
import {
  Box,
  Typography,
  IconButton,
  Chip,
  useTheme,
  alpha,
  Tooltip,
  List,
  ListItem,
  ListItemText,
  Badge,
} from '@mui/material';
import {
  Delete as DeleteIcon,
  Download as DownloadIcon,
} from '@mui/icons-material';

const LogViewer = ({ logs, onClearLogs, isConnected }) => {
  const theme = useTheme();
  const logContainerRef = useRef(null);

  useEffect(() => {
    // Auto-scroll to bottom when new logs arrive
    if (logContainerRef.current) {
      logContainerRef.current.scrollTop = logContainerRef.current.scrollHeight;
    }
  }, [logs]);

  const getLogLevelColor = (level) => {
    switch (level?.toLowerCase()) {
      case 'error':
        return {
          color: theme.palette.error.main,
          backgroundColor: alpha(theme.palette.error.main, 0.1),
          borderColor: theme.palette.error.main,
        };
      case 'warning':
        return {
          color: theme.palette.warning.main,
          backgroundColor: alpha(theme.palette.warning.main, 0.1),
          borderColor: theme.palette.warning.main,
        };
      case 'info':
        return {
          color: theme.palette.info.main,
          backgroundColor: alpha(theme.palette.info.main, 0.1),
          borderColor: theme.palette.info.main,
        };
      case 'debug':
        return {
          color: theme.palette.grey[600],
          backgroundColor: alpha(theme.palette.grey[500], 0.1),
          borderColor: theme.palette.grey[400],
        };
      default:
        return {
          color: theme.palette.text.primary,
          backgroundColor: theme.palette.background.paper,
          borderColor: theme.palette.divider,
        };
    }
  };

  const getLogLevelChipColor = (level) => {
    switch (level?.toLowerCase()) {
      case 'error':
        return 'error';
      case 'warning':
        return 'warning';
      case 'info':
        return 'info';
      case 'debug':
        return 'default';
      default:
        return 'default';
    }
  };

  const formatTimestamp = (timestamp) => {
    return new Date(timestamp).toLocaleTimeString();
  };

  const downloadLogs = () => {
    const logText = logs.map(log => 
      `[${formatTimestamp(log.timestamp)}] ${log.level?.toUpperCase() || 'INFO'}: ${log.message}`
    ).join('\n');
    
    const blob = new Blob([logText], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `property-alert-logs-${new Date().toISOString().split('T')[0]}.txt`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <Box sx={{ height: 500, display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          p: 2,
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="h6" component="h3" fontWeight="bold">
            Activity Logs
          </Typography>
          <Badge
            color={isConnected ? 'success' : 'error'}
            variant="dot"
            sx={{
              '& .MuiBadge-badge': {
                width: 8,
                height: 8,
                borderRadius: '50%',
              },
            }}
          />
        </Box>
        
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          <Tooltip title="Download Logs">
            <span>
              <IconButton
                onClick={downloadLogs}
                disabled={logs.length === 0}
                size="small"
                color="primary"
              >
                <DownloadIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
          
          <Tooltip title="Clear Logs">
            <span>
              <IconButton
                onClick={onClearLogs}
                disabled={logs.length === 0}
                size="small"
                color="error"
              >
                <DeleteIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
        </Box>
      </Box>

      {/* Log Content */}
      <Box
        ref={logContainerRef}
        sx={{
          flexGrow: 1,
          overflow: 'auto',
          p: 1,
          bgcolor: alpha(theme.palette.background.default, 0.5),
        }}
      >
        {logs.length === 0 ? (
          <Box
            sx={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              height: '100%',
              textAlign: 'center',
              color: 'text.secondary',
            }}
          >
            <Typography variant="body1" gutterBottom>
              No logs yet
            </Typography>
            <Typography variant="caption">
              {isConnected ? 'Waiting for activity...' : 'Disconnected from log stream'}
            </Typography>
          </Box>
        ) : (
          <List dense sx={{ py: 0 }}>
            {logs.map((log, index) => {
              const levelStyle = getLogLevelColor(log.level);
              return (
                <ListItem
                  key={index}
                  sx={{
                    mb: 1,
                    p: 1.5,
                    borderRadius: 1,
                    borderLeft: 3,
                    borderLeftColor: levelStyle.borderColor,
                    backgroundColor: levelStyle.backgroundColor,
                    color: levelStyle.color,
                    fontFamily: 'monospace',
                    fontSize: '0.75rem',
                    alignItems: 'flex-start',
                  }}
                >
                  <ListItemText
                    primary={
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                        <Typography
                          variant="caption"
                          component="span"
                          sx={{ fontFamily: 'monospace', fontWeight: 'bold' }}
                        >
                          [{formatTimestamp(log.timestamp)}]
                        </Typography>
                        <Chip
                          label={log.level?.toUpperCase() || 'INFO'}
                          size="small"
                          color={getLogLevelChipColor(log.level)}
                          variant="outlined"
                          sx={{ height: 20, fontSize: '0.6rem' }}
                        />
                      </Box>
                    }
                    secondary={
                      <Box>
                        <Typography
                          variant="body2"
                          component="div"
                          sx={{
                            fontFamily: 'monospace',
                            fontSize: '0.75rem',
                            whiteSpace: 'pre-wrap',
                            wordBreak: 'break-word',
                            mt: 0.5,
                          }}
                        >
                          {log.message}
                        </Typography>
                        {log.details && (
                          <Typography
                            variant="caption"
                            component="div"
                            sx={{
                              fontFamily: 'monospace',
                              opacity: 0.8,
                              mt: 0.5,
                              whiteSpace: 'pre-wrap',
                              wordBreak: 'break-word',
                            }}
                          >
                            {typeof log.details === 'string' 
                              ? log.details 
                              : JSON.stringify(log.details, null, 2)
                            }
                          </Typography>
                        )}
                      </Box>
                    }
                  />
                </ListItem>
              );
            })}
          </List>
        )}
      </Box>

      {/* Footer */}
      <Box
        sx={{
          px: 2,
          py: 1,
          borderTop: 1,
          borderColor: 'divider',
          bgcolor: 'background.paper',
        }}
      >
        <Typography variant="caption" color="text.secondary">
          {logs.length} log entries • {isConnected ? 'Live' : 'Offline'}
        </Typography>
      </Box>
    </Box>
  );
};

export default LogViewer;
