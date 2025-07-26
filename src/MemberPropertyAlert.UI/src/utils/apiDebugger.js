// API Debugging Utility
// This utility helps diagnose API connectivity issues

import config from '../config/apiConfig';

export const debugApiConnectivity = async () => {
  console.log('🔍 Starting API connectivity debug...');
  console.log('🔍 Current config:', config);
  
  const results = {
    baseUrl: config.apiBaseUrl,
    tests: []
  };
  
  // Test endpoints to check
  const endpoints = [
    { name: 'Health Check', path: '/health' },
    { name: 'Dashboard Stats', path: '/dashboard/stats' },
    { name: 'Dashboard Recent Activity', path: '/dashboard/recent-activity' },
    { name: 'Dashboard Health', path: '/dashboard/health' }
  ];
  
  for (const endpoint of endpoints) {
    const url = `${config.apiBaseUrl}${endpoint.path}`;
    console.log(`🔍 Testing ${endpoint.name}: ${url}`);
    
    try {
      const startTime = Date.now();
      const response = await fetch(url, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json'
        },
        mode: 'cors'
      });
      
      const endTime = Date.now();
      const responseTime = endTime - startTime;
      
      const testResult = {
        name: endpoint.name,
        url: url,
        status: response.status,
        statusText: response.statusText,
        ok: response.ok,
        responseTime: responseTime,
        headers: Object.fromEntries(response.headers.entries())
      };
      
      if (response.ok) {
        try {
          const data = await response.json();
          testResult.data = data;
          console.log(`✅ ${endpoint.name} - SUCCESS (${responseTime}ms):`, data);
        } catch (jsonError) {
          testResult.jsonError = jsonError.message;
          console.log(`✅ ${endpoint.name} - SUCCESS but invalid JSON (${responseTime}ms)`);
        }
      } else {
        console.log(`❌ ${endpoint.name} - FAILED: ${response.status} ${response.statusText} (${responseTime}ms)`);
      }
      
      results.tests.push(testResult);
      
    } catch (error) {
      const testResult = {
        name: endpoint.name,
        url: url,
        error: error.message,
        type: error.name
      };
      
      console.log(`❌ ${endpoint.name} - ERROR:`, error);
      results.tests.push(testResult);
    }
  }
  
  console.log('🔍 API Debug Results:', results);
  return results;
};

// Test different URL patterns
export const testUrlPatterns = () => {
  console.log('🔍 Testing URL pattern detection...');
  
  const currentLocation = {
    href: window.location.href,
    hostname: window.location.hostname,
    port: window.location.port,
    protocol: window.location.protocol,
    pathname: window.location.pathname
  };
  
  console.log('🔍 Current location:', currentLocation);
  
  // Test different hostname patterns
  const testHostnames = [
    'web-mpa-dev-eus2-6h6.azurewebsites.net',
    'mpa-web-dev-eus2.azurewebsites.net',
    'memberpropertyalert-ui.azurewebsites.net',
    'localhost:3000',
    '127.0.0.1:3000'
  ];
  
  testHostnames.forEach(hostname => {
    console.log(`🔍 Testing hostname pattern: ${hostname}`);
    
    let functionAppUrl;
    if (hostname.includes('azurewebsites.net')) {
      if (hostname.startsWith('web-')) {
        functionAppUrl = hostname.replace('web-', 'func-');
      } else if (hostname.includes('-web-')) {
        functionAppUrl = hostname.replace('-web-', '-func-');
      } else {
        const parts = hostname.split('.');
        if (parts.length >= 2) {
          const appName = parts[0];
          functionAppUrl = `${appName.replace(/ui$|web$/, 'func')}.azurewebsites.net`;
        } else {
          functionAppUrl = hostname.replace('ui', 'func');
        }
      }
      
      const apiUrl = `https://${functionAppUrl}/api`;
      console.log(`   → Function App URL: ${apiUrl}`);
    } else if (hostname.includes('localhost') || hostname.includes('127.0.0.1')) {
      const [host] = hostname.split(':');
      const functionPort = '7071';
      const apiUrl = `http://${host}:${functionPort}/api`;
      console.log(`   → Local Function App URL: ${apiUrl}`);
    }
  });
};

// Export for global access in browser console
if (typeof window !== 'undefined') {
  window.debugApiConnectivity = debugApiConnectivity;
  window.testUrlPatterns = testUrlPatterns;
}
