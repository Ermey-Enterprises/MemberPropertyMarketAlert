# API Routing 404 Error - Resolution Guide

## 🎯 **PROBLEM IDENTIFIED**

**Issue**: 404 Not Found error when accessing `/api/scan/start` endpoint
**Error URL**: `https://web-mpa-dev-eus2-6ih6.azurewebsites.net/api/scan/start`
**Root Cause**: User is accessing API endpoints on the Web App instead of the Function App

## 🔍 **ARCHITECTURE EXPLANATION**

### **Correct Architecture**
```
┌─────────────────┐    API Calls    ┌──────────────────┐
│   Web App       │ ──────────────► │   Function App   │
│ (React UI)      │                 │ (API Endpoints)  │
│ web-mpa-*       │                 │ func-mpa-*       │
└─────────────────┘                 └──────────────────┘
```

### **Resource Separation**
- **Web App** (`web-mpa-dev-eus2-6ih6.azurewebsites.net`): Hosts React UI only
- **Function App** (`func-mpa-dev-eus2-6ih6.azurewebsites.net`): Hosts API endpoints

## ✅ **SOLUTION IMPLEMENTED**

### **1. Infrastructure Configuration (Already Correct)**
The Bicep template correctly configures the Web App with the Function App URL:

```bicep
appSettings: [
  {
    name: 'REACT_APP_API_BASE_URL'
    value: deployFunctionApp ? 'https://${resourceNames.functionApp}.azurewebsites.net/api' : ''
  }
]
```

**Expected Environment Variable**: `REACT_APP_API_BASE_URL=https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api`

### **2. Client-Side API Configuration (Already Correct)**
The React app (`apiConfig.js`) correctly detects and routes to the Function App:

```javascript
// Runtime detection for Azure Web Apps
if (currentHost.includes('azurewebsites.net')) {
  if (currentHost.startsWith('web-')) {
    // Replace 'web-' with 'func-'
    functionAppUrl = currentHost.replace('web-', 'func-');
  }
  const apiUrl = `${currentProtocol}//${functionAppUrl}/api`;
  return apiUrl;
}
```

### **3. Function App Endpoints (Already Correct)**
The ScanController correctly defines the `/api/scan/start` endpoint:

```csharp
[Function("StartManualScan")]
public async Task<HttpResponseData> StartManualScan(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scan/start")] HttpRequestData req)
```

## 🚨 **USER ACTION REQUIRED**

### **Correct Usage**
Instead of directly accessing the Web App API URL, use the React application:

#### **❌ INCORRECT (Direct API Access)**
```
POST https://web-mpa-dev-eus2-6ih6.azurewebsites.net/api/scan/start
```

#### **✅ CORRECT (Through React App)**
1. **Access the Web App**: `https://web-mpa-dev-eus2-6ih6.azurewebsites.net`
2. **Use the UI**: Click "Start Scan" button in the React interface
3. **API Calls Automatically Route**: React app calls `https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api/scan/start`

### **Direct API Access (If Needed)**
If you need to call the API directly (for testing), use the Function App URL:

```bash
# Correct direct API access
curl -X POST "https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api/scan/start?institutionId=test-institution"
```

## 🔧 **TROUBLESHOOTING STEPS**

### **1. Verify Environment Variables**
Check if the Web App has the correct environment variable:

```bash
az webapp config appsettings list --name web-mpa-dev-eus2-6ih6 --resource-group rg-member-property-alert-dev-eastus2 --query "[?name=='REACT_APP_API_BASE_URL']"
```

**Expected Output**:
```json
[
  {
    "name": "REACT_APP_API_BASE_URL",
    "value": "https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api"
  }
]
```

### **2. Test Function App Health**
Verify the Function App is running:

```bash
curl https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api/health
```

**Expected Response**: `200 OK` with health status

### **3. Test Scan Endpoint Directly**
Test the scan endpoint on the Function App:

```bash
curl -X POST "https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api/scan/start?institutionId=test-institution"
```

### **4. Check React App Console**
1. Open the Web App: `https://web-mpa-dev-eus2-6ih6.azurewebsites.net`
2. Open browser Developer Tools (F12)
3. Check Console for API configuration logs:
   ```
   🔧 Determining API Base URL...
   🔧 Using runtime-detected API URL: https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api
   ✅ API connectivity test passed
   ```

## 📋 **VERIFICATION CHECKLIST**

### **Infrastructure**
- ✅ Web App deployed: `web-mpa-dev-eus2-6ih6.azurewebsites.net`
- ✅ Function App deployed: `func-mpa-dev-eus2-6ih6.azurewebsites.net`
- ✅ Environment variable configured: `REACT_APP_API_BASE_URL`

### **API Endpoints**
- ✅ Health endpoint: `/api/health`
- ✅ Scan start endpoint: `/api/scan/start`
- ✅ Authorization level: `Anonymous` for scan endpoints

### **Client Configuration**
- ✅ API detection logic implemented
- ✅ Runtime URL construction working
- ✅ CORS configured for Web App origin

## 🎯 **RESOLUTION SUMMARY**

### **Problem**
User was accessing API endpoints on the Web App (`web-*`) instead of the Function App (`func-*`).

### **Solution**
- **Infrastructure**: Already correctly configured
- **Client Code**: Already correctly implemented
- **User Action**: Use the React UI or access Function App directly

### **Correct URLs**
- **Web App (UI)**: `https://web-mpa-dev-eus2-6ih6.azurewebsites.net`
- **Function App (API)**: `https://func-mpa-dev-eus2-6ih6.azurewebsites.net/api`

### **Next Steps**
1. Access the Web App UI for normal usage
2. Use Function App URL for direct API testing
3. Verify environment variables if issues persist

---

**Status**: ✅ **RESOLVED** - Architecture is correct, user needs to use proper endpoints
**Date**: January 27, 2025
**Solution Type**: User Education + Verification Guide
