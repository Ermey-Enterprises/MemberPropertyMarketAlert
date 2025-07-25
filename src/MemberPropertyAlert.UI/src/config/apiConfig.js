// API Configuration Service
// This handles both build-time and runtime configuration

const getApiBaseUrl = () => {
  // First try build-time environment variable
  const buildTimeUrl = process.env.REACT_APP_API_BASE_URL;
  if (buildTimeUrl && buildTimeUrl !== '') {
    console.log('🔧 Using build-time API URL:', buildTimeUrl);
    return buildTimeUrl;
  }

  // Fallback to runtime detection based on current host
  const currentHost = window.location.hostname;
  
  // If running on Azure Web App, construct the Function App URL
  if (currentHost.includes('azurewebsites.net')) {
    // Replace 'web-' with 'func-' to get Function App URL
    const functionAppUrl = currentHost.replace('web-', 'func-');
    const apiUrl = `https://${functionAppUrl}/api`;
    console.log('🔧 Using runtime-detected API URL:', apiUrl);
    return apiUrl;
  }

  // Default for local development
  const localUrl = 'http://localhost:7071/api';
  console.log('🔧 Using local development API URL:', localUrl);
  return localUrl;
};

const config = {
  apiBaseUrl: getApiBaseUrl(),
  environment: process.env.REACT_APP_ENVIRONMENT || 'development'
};

console.log('🚀 API Configuration loaded:', config);

export default config;
