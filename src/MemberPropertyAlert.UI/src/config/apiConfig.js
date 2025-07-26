// API Configuration Service
// This handles both build-time and runtime configuration

const getApiBaseUrl = () => {
  console.log('🔧 Determining API Base URL...');
  console.log('🔧 Current location:', window.location.href);
  console.log('🔧 Current hostname:', window.location.hostname);
  
  // First try build-time environment variable
  const buildTimeUrl = process.env.REACT_APP_API_BASE_URL;
  if (buildTimeUrl && buildTimeUrl !== '' && buildTimeUrl !== 'undefined') {
    console.log('🔧 Using build-time API URL:', buildTimeUrl);
    return buildTimeUrl.replace(/\/+$/, ''); // Remove trailing slashes
  }

  // Fallback to runtime detection based on current host
  const currentHost = window.location.hostname;
  const currentProtocol = window.location.protocol;
  
  // If running on Azure Web App, construct the Function App URL
  if (currentHost.includes('azurewebsites.net')) {
    let functionAppUrl;
    
    // Try multiple patterns for Function App URL detection
    if (currentHost.startsWith('web-')) {
      // Replace 'web-' with 'func-'
      functionAppUrl = currentHost.replace('web-', 'func-');
    } else if (currentHost.includes('-web-')) {
      // Replace '-web-' with '-func-'
      functionAppUrl = currentHost.replace('-web-', '-func-');
    } else {
      // Fallback: try to construct based on naming convention
      const parts = currentHost.split('.');
      if (parts.length >= 2) {
        const appName = parts[0];
        functionAppUrl = `${appName.replace(/ui$|web$/, 'func')}.azurewebsites.net`;
      } else {
        functionAppUrl = currentHost.replace('ui', 'func');
      }
    }
    
    const apiUrl = `${currentProtocol}//${functionAppUrl}/api`;
    console.log('🔧 Using runtime-detected API URL:', apiUrl);
    return apiUrl;
  }

  // Check if we're running on localhost with a different port
  if (currentHost === 'localhost' || currentHost === '127.0.0.1') {
    // Try to detect if Function App is running on a different port
    const currentPort = window.location.port;
    let functionPort = '7071'; // Default Azure Functions port
    
    // If UI is on 3000, Functions likely on 7071
    // If UI is on different port, try common Function ports
    if (currentPort === '3000') {
      functionPort = '7071';
    } else if (currentPort === '5000') {
      functionPort = '7071';
    }
    
    const localUrl = `http://${currentHost}:${functionPort}/api`;
    console.log('🔧 Using local development API URL:', localUrl);
    return localUrl;
  }

  // Final fallback
  const fallbackUrl = 'http://localhost:7071/api';
  console.warn('⚠️ Could not determine API URL, using fallback:', fallbackUrl);
  return fallbackUrl;
};

// Test API connectivity
const testApiConnectivity = async (baseUrl) => {
  try {
    console.log('🧪 Testing API connectivity to:', baseUrl);
    const response = await fetch(`${baseUrl}/health`, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
    });
    
    if (response.ok) {
      console.log('✅ API connectivity test passed');
      return true;
    } else {
      console.warn('⚠️ API connectivity test failed with status:', response.status);
      return false;
    }
  } catch (error) {
    console.warn('⚠️ API connectivity test failed:', error.message);
    return false;
  }
};

const config = {
  apiBaseUrl: getApiBaseUrl(),
  environment: process.env.REACT_APP_ENVIRONMENT || 'development',
  testConnectivity: testApiConnectivity
};

console.log('🚀 API Configuration loaded:', config);

// Test connectivity on load (non-blocking)
setTimeout(() => {
  config.testConnectivity(config.apiBaseUrl).catch(err => {
    console.warn('⚠️ Initial API connectivity test failed:', err);
  });
}, 1000);

export default config;
