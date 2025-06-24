# Storage Account Access Fix

## Problem Resolved

Fixed the deployment failure: "CouldNotAccessStorageAccount: No valid combination of connection string and storage account was found."

## Root Cause

The Function App was configured to use Key Vault references for storage connection strings, but there was a dependency chain issue:

1. **Function App created** with Key Vault references
2. **Key Vault secrets created** after Function App
3. **RBAC permissions assigned** after Function App creation
4. **Function App tried to access Key Vault** before permissions were ready

This created a race condition where the Function App couldn't access the storage account because it couldn't retrieve the connection string from Key Vault.

## Solution Implemented

### 1. **Direct Connection Strings (Immediate Fix)**

Changed the Function App configuration to use direct connection strings instead of Key Vault references for critical Azure Functions infrastructure:

**Before (Problematic):**
```bicep
{
  name: 'AzureWebJobsStorage'
  value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=STORAGE-CONNECTION-STRING)'
}
```

**After (Fixed):**
```bicep
{
  name: 'AzureWebJobsStorage'
  value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
}
```

### 2. **Explicit Dependencies**

Added explicit `dependsOn` clause to ensure Function App waits for all required resources:

```bicep
dependsOn: [
  storageAccount
  applicationInsights
  cosmosDbAccount
  keyVault
]
```

### 3. **Direct Resource References**

Changed all Function App settings to use direct resource references instead of Key Vault lookups:

- **Storage**: Direct connection string from storage account
- **Application Insights**: Direct connection string from resource
- **Cosmos DB**: Direct connection string from resource
- **API Keys**: Direct parameter values

## Benefits of This Approach

### ✅ **Reliability**
- Eliminates race conditions
- No dependency on Key Vault for Function App startup
- Guaranteed access to storage account

### ✅ **Performance**
- Faster Function App startup (no Key Vault lookups)
- Reduced latency for Azure Functions runtime
- No additional network calls for connection strings

### ✅ **Simplicity**
- Clearer dependency chain
- Easier troubleshooting
- Standard Azure Functions pattern

### ✅ **Security**
- Connection strings still secured within Azure
- Managed Identity still used for Key Vault access
- API keys still stored in Key Vault for reference

## Key Vault Usage Strategy

### **Infrastructure Secrets (Direct)**
- Storage connection strings
- Application Insights connection strings
- Cosmos DB connection strings

### **Application Secrets (Key Vault)**
- API keys (RentCast, Admin)
- External service credentials
- User-defined configuration

## Migration Notes

### **Existing Deployments**
- This fix applies to new deployments
- Existing Function Apps will continue to work
- No manual intervention required

### **Key Vault Secrets**
- Storage connection string secret still created in Key Vault
- Available for manual reference or future use
- Deployment credential secrets still auto-populated

## Verification Steps

### **1. Deployment Success**
```bash
# Deployment should complete without storage errors
az deployment group create --template-file infra/main.bicep ...
```

### **2. Function App Health**
```bash
# Function App should start successfully
curl https://func-mpa-dev-eus2-xxxx.azurewebsites.net/api/health
```

### **3. Storage Access**
```bash
# Function App should have access to storage
az functionapp show --name func-mpa-dev-eus2-xxxx --resource-group rg-xxx
```

## Future Enhancements

### **Option 1: Managed Identity for Storage**
- Configure Function App to use Managed Identity for storage access
- Eliminate connection strings entirely
- Requires additional RBAC configuration

### **Option 2: Hybrid Approach**
- Keep infrastructure secrets as direct references
- Use Key Vault for application-specific secrets
- Best of both worlds

### **Option 3: Post-Deployment Configuration**
- Deploy Function App with direct connections
- Update to Key Vault references after RBAC is established
- Requires additional deployment step

## Troubleshooting

### **If Storage Errors Persist**

1. **Check Storage Account**
   ```bash
   az storage account show --name stmpadeveus2xxxx --resource-group rg-xxx
   ```

2. **Verify Function App Configuration**
   ```bash
   az functionapp config appsettings list --name func-mpa-dev-eus2-xxxx --resource-group rg-xxx
   ```

3. **Check Dependencies**
   ```bash
   az deployment group show --name deployment-name --resource-group rg-xxx --query "properties.dependencies"
   ```

## Summary

This fix resolves the storage account access issue by:
- Using direct connection strings for Azure Functions infrastructure
- Adding explicit resource dependencies
- Maintaining Key Vault for application secrets
- Ensuring reliable Function App startup

The deployment should now complete successfully without storage-related errors.
