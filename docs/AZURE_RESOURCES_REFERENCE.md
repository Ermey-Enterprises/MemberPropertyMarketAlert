# Azure Resources Reference - Member Property Market Alert

## 📋 **Project Resource Overview**

This document provides a comprehensive reference for all Azure resources used by the Member Property Market Alert project, including deployment behavior and cleanup guidance.

---

## 🏗️ **Resource Group Configuration**

### **Current Active Resource Group**
- **Name**: `rg-member-property-alert-dev`
- **Location**: East US
- **Environment**: Development
- **CI/CD Behavior**: ✅ Used by all workflows
- **Cleanup**: ⚠️ **DO NOT DELETE** - Contains active resources

### **Resource Group Naming Patterns**
```
Development:  rg-member-property-alert-dev
Testing:      rg-member-property-alert-test (if exists)
Production:   rg-member-property-alert-prod (if exists)
```

---

## 🔧 **Infrastructure Resources**

### **1. Log Analytics Workspace**
- **Name**: `log-member-property-alert-dev`
- **Type**: `Microsoft.OperationalInsights/workspaces`
- **SKU**: PerGB2018
- **Data Retention**: 30 days (dev/test), 90 days (prod)
- **Purpose**: Centralized logging for all services
- **CI/CD Behavior**: 
  - ✅ **Idempotent** - Updates configuration if exists
  - ✅ **Data Preserved** - Historical logs maintained
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: ⚠️ **MEDIUM** - Loses historical logs

### **2. Application Insights**
- **Name**: `ai-member-property-alert-dev`
- **Type**: `Microsoft.Insights/components`
- **Kind**: Web application
- **Purpose**: Application performance monitoring, telemetry
- **Dependencies**: Links to Log Analytics Workspace
- **CI/CD Behavior**:
  - ✅ **Idempotent** - Preserves existing telemetry
  - ✅ **Data Preserved** - Performance metrics maintained
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: ⚠️ **MEDIUM** - Loses performance history

---

## 💾 **Data & Storage Resources**

### **3. Storage Account**
- **Name**: `stmemberpropertyalertdev{uniqueSuffix}`
- **Type**: `Microsoft.Storage/storageAccounts`
- **SKU**: Standard_LRS
- **Purpose**: Function App storage, deployment packages, logs
- **Contains**: Function binaries, configuration, temporary data
- **CI/CD Behavior**:
  - ✅ **Idempotent** - Preserves existing data
  - ✅ **Data Preserved** - Files and blobs maintained
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: ⚠️ **MEDIUM** - Function App requires this

### **4. Cosmos DB Account**
- **Name**: `cosmos-member-property-alert-dev`
- **Type**: `Microsoft.DocumentDB/databaseAccounts`
- **Configuration**: Serverless, Session consistency
- **Free Tier**: Enabled (dev/test only)
- **Purpose**: Primary application database
- **CI/CD Behavior**:
  - ✅ **Idempotent** - Preserves all data
  - ✅ **Data Preserved** - All documents maintained
  - ⚠️ **Settings Updated** - Configuration may change
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: 🔴 **HIGH** - Contains all application data

### **5. Cosmos DB Database**
- **Name**: `MemberPropertyAlert`
- **Parent**: Cosmos DB Account
- **Purpose**: Application database container
- **CI/CD Behavior**:
  - ✅ **Idempotent** - Created if missing
  - ✅ **Data Preserved** - All collections maintained
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: 🔴 **HIGH** - Contains all application data

### **6. Cosmos DB Containers** (4 containers)

#### **Institutions Container**
- **Name**: `Institutions`
- **Partition Key**: `/id`
- **Purpose**: Financial institution data
- **Data**: Institution profiles, settings, configurations

#### **Addresses Container**
- **Name**: `Addresses`
- **Partition Key**: `/institutionId`
- **Purpose**: Member property addresses to monitor
- **Data**: Property addresses, monitoring status

#### **Alerts Container**
- **Name**: `Alerts`
- **Partition Key**: `/institutionId`
- **Purpose**: Generated property listing alerts
- **Data**: Alert history, notifications sent

#### **ScanLogs Container**
- **Name**: `ScanLogs`
- **Partition Key**: `/institutionId`
- **Purpose**: Property scanning activity logs
- **Data**: Scan results, API call logs, timestamps

**CI/CD Behavior (All Containers)**:
- ✅ **Idempotent** - Created if missing, preserves data
- ✅ **Schema Preserved** - Partition keys and indexing maintained
- ✅ **Data Preserved** - All documents maintained
- 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: 🔴 **HIGH** - Contains all application data

---

## 🖥️ **Compute Resources**

### **7. App Service Plan**
- **Name**: `asp-member-property-alert-dev` (if using new naming) OR `EastUSPlan` (current)
- **Type**: `Microsoft.Web/serverfarms`
- **SKU**: B1 (dev/test), P1v3 (production)
- **Platform**: Linux
- **Purpose**: Hosts Function App
- **CI/CD Behavior**:
  - ✅ **Idempotent** - Updates SKU if changed
  - ⚠️ **Brief Restart** - When SKU changes
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: ⚠️ **MEDIUM** - Function App depends on this

### **8. Function App**
- **Name**: `func-member-property-alert-dev`
- **Type**: `Microsoft.Web/sites` (functionapp,linux)
- **Runtime**: .NET 8.0 Isolated
- **Purpose**: Main application backend (REST API, scheduled tasks)
- **Features**: Timer triggers, HTTP triggers, Cosmos DB bindings
- **CI/CD Behavior**:
  - 🔄 **Code Deployed Every CI/CD Run** - Application updates
  - ⚠️ **Brief Downtime** - During deployment (~30-60 seconds)
  - ✅ **Configuration Preserved** - App settings maintained
  - ✅ **Identity Preserved** - Managed identity maintained
- **Cleanup Risk**: 🔴 **HIGH** - Core application component

### **9. Static Web App**
- **Name**: `swa-member-property-alert-dev`
- **Type**: `Microsoft.Web/staticSites`
- **Location**: Central US (limited regional availability)
- **SKU**: Free tier
- **Purpose**: Admin dashboard/UI
- **Source**: GitHub repository integration
- **CI/CD Behavior**:
  - 🔄 **Rebuilt Every CI/CD Run** - UI updates
  - ⚠️ **Brief Downtime** - During build/deploy (~2-5 minutes)
  - ✅ **GitHub Integration Preserved** - Repository links maintained
- **Cleanup Risk**: ⚠️ **MEDIUM** - Admin interface only

---

## 🔐 **Security & Access Resources**

### **10. System-Assigned Managed Identity**
- **Resource**: Function App
- **Type**: Automatic Azure AD identity
- **Purpose**: Secure access to Cosmos DB (no connection strings)
- **Permissions**: Cosmos DB Data Contributor role
- **CI/CD Behavior**:
  - ✅ **Idempotent** - Preserved across deployments
  - ✅ **Permissions Preserved** - Role assignments maintained
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: ⚠️ **MEDIUM** - Function App needs Cosmos DB access

### **11. Role Assignments**
- **Type**: `Microsoft.Authorization/roleAssignments`
- **Assignment**: Function App → Cosmos DB Data Contributor
- **Purpose**: Allow Function App to read/write Cosmos DB
- **CI/CD Behavior**:
  - ✅ **Post-deployment script** - Applied after infrastructure
  - ✅ **Idempotent** - No duplicates created
  - 🔄 **Redeploys**: Only on infrastructure changes
- **Cleanup Risk**: 🔴 **HIGH** - Function App cannot access data without this

---

## 🚀 **CI/CD Deployment Behavior**

### **Deployment Triggers**
1. **Infrastructure Changes**: `/infra/` files modified
2. **Application Changes**: Source code in `/src/` modified  
3. **Manual Trigger**: Workflow dispatch

### **Deployment Flow**
```
Infrastructure (Bicep) → Function App Code → Static Web App → Role Assignments
     ↓                        ↓                  ↓              ↓
  Idempotent            Brief Downtime     Brief Downtime   Post-Deploy
  Safe Updates         (~30-60 seconds)   (~2-5 minutes)    Idempotent
```

### **What Gets Deployed Every Time**
- 🔄 **Function App Code** - Always deployed
- 🔄 **Static Web App** - Always rebuilt
- 🔄 **Infrastructure** - Only if `/infra/` files changed

### **What's Preserved**
- ✅ **All Cosmos DB Data** - Documents, containers, database
- ✅ **Storage Account Data** - Files, blobs, configuration
- ✅ **Application Insights Data** - Telemetry, performance metrics
- ✅ **Log Analytics Data** - Historical logs
- ✅ **Managed Identity** - Security permissions
- ✅ **App Settings** - Function App configuration

---

## 🧹 **Resource Cleanup Guide**

### **Safe to Delete (Development Environment)**
```powershell
# List all resources in the dev environment
az resource list --resource-group "rg-member-property-alert-dev" --output table

# Delete entire development environment (DESTRUCTIVE)
az group delete --name "rg-member-property-alert-dev" --yes --no-wait
```

### **Identify Resources by Project Tags**
```powershell
# Find resources with project tags
az resource list --tag Application=MemberPropertyAlert --output table
az resource list --tag ManagedBy=Bicep --output table
```

### **Individual Resource Cleanup**
```powershell
# Delete specific resources (examples)
az staticapp delete --name "swa-member-property-alert-dev" --resource-group "rg-member-property-alert-dev"
az functionapp delete --name "func-member-property-alert-dev" --resource-group "rg-member-property-alert-dev"
```

### **Data Backup Before Cleanup**
```powershell
# Backup Cosmos DB data
az cosmosdb sql container throughput show --account-name "cosmos-member-property-alert-dev" --database-name "MemberPropertyAlert" --name "Institutions" --resource-group "rg-member-property-alert-dev"

# Export Application Insights data
# (Use Azure portal or PowerBI for historical data export)
```

---

## ⚠️ **Cleanup Risk Assessment**

### **🔴 HIGH RISK - DO NOT DELETE ACCIDENTALLY**
- **Cosmos DB Account & Containers** - Contains all application data
- **Function App** - Core application logic
- **Role Assignments** - Required for data access

### **⚠️ MEDIUM RISK - CAUSES SERVICE DISRUPTION**
- **Storage Account** - Function App dependencies
- **App Service Plan** - Hosts Function App
- **Application Insights** - Monitoring capabilities
- **Log Analytics** - Historical logs

### **🟡 LOW RISK - UI/ADMIN ONLY**
- **Static Web App** - Admin dashboard (can be recreated)

---

## 📍 **Current Resource Inventory**

Based on the actual resources found in `rg-member-property-alert-dev`:

| Resource Name | Type | Status | Purpose |
|---------------|------|--------|---------|
| `cosmos-member-property-alert-dev` | Cosmos DB | 🟢 Active | Database |
| `stmemberpropertyalertdev` | Storage Account | 🟢 Active | Function Storage |
| `func-member-property-alert-dev` | Function App | 🟢 Active | Backend API |
| `func-member-property-alert-dev` | App Insights | 🟢 Active | Monitoring |
| `EastUSPlan` | App Service Plan | 🟢 Active | Compute Host |
| `Application Insights Smart Detection` | Action Group | 🟢 Active | Auto-monitoring |

**All resources are actively used and should be preserved.**
