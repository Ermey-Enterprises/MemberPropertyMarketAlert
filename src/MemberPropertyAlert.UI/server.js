const express = require('express');
const path = require('path');
const app = express();

// Use PORT environment variable or default to 8080 (Azure App Service default)
const port = process.env.PORT || 8080;

// Add logging for debugging
console.log('🚀 Member Property Alert UI Server Starting...');
console.log(`🔌 Port: ${port}`);
console.log(`📁 Directory: ${__dirname}`);
console.log(`📁 Build path: ${path.join(__dirname, 'build')}`);

// Health check endpoint
app.get('/health', (req, res) => {
  res.status(200).json({ 
    status: 'healthy', 
    timestamp: new Date().toISOString(),
    port: port 
  });
});

// Serve static files from the React app build directory
app.use(express.static(path.join(__dirname, 'build')));

// Handle React routing, return all requests to React app
app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, 'build', 'index.html'));
});

// Start server
const server = app.listen(port, '0.0.0.0', () => {
  console.log(`✅ Server is running on http://0.0.0.0:${port}`);
  console.log(`📊 Health check available at http://0.0.0.0:${port}/health`);
});

// Graceful shutdown
process.on('SIGTERM', () => {
  console.log('🛑 SIGTERM received, shutting down gracefully...');
  server.close(() => {
    console.log('✅ Server closed');
    process.exit(0);
  });
});

process.on('SIGINT', () => {
  console.log('🛑 SIGINT received, shutting down gracefully...');
  server.close(() => {
    console.log('✅ Server closed');
    process.exit(0);
  });
});
