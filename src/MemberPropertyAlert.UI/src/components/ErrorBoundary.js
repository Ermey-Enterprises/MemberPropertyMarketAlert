import React from 'react';
import { Box, Typography, Button, Paper } from '@mui/material';

class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null, errorInfo: null };
  }

  static getDerivedStateFromError(error) {
    // Update state so the next render will show the fallback UI
    return { hasError: true };
  }

  componentDidCatch(error, errorInfo) {
    // Log the error details
    console.error('🚨 React Error Boundary caught an error:', error);
    console.error('🚨 Error Info:', errorInfo);
    console.error('🚨 Component Stack:', errorInfo.componentStack);
    
    this.setState({
      error: error,
      errorInfo: errorInfo
    });
  }

  render() {
    if (this.state.hasError) {
      return (
        <Box sx={{ p: 3, minHeight: '100vh', bgcolor: 'background.default' }}>
          <Paper sx={{ p: 4, maxWidth: 800, mx: 'auto' }}>
            <Typography variant="h4" color="error" gutterBottom>
              🚨 Application Error
            </Typography>
            <Typography variant="body1" paragraph>
              The application encountered an error and crashed. Please check the browser console for details.
            </Typography>
            
            {this.state.error && (
              <Box sx={{ mt: 3 }}>
                <Typography variant="h6" gutterBottom>
                  Error Details:
                </Typography>
                <Box
                  component="pre"
                  sx={{
                    bgcolor: 'grey.100',
                    p: 2,
                    borderRadius: 1,
                    overflow: 'auto',
                    fontSize: '0.875rem',
                    fontFamily: 'monospace'
                  }}
                >
                  {this.state.error.toString()}
                  {this.state.errorInfo.componentStack}
                </Box>
              </Box>
            )}
            
            <Button
              variant="contained"
              onClick={() => window.location.reload()}
              sx={{ mt: 3 }}
            >
              Reload Page
            </Button>
          </Paper>
        </Box>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
