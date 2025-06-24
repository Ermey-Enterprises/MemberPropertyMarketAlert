# Storage Permissions Fix - Managed Identity Approach

## Issue
The Azure Functions deployment was failing with a 403 Forbidden error when trying to access the Azure Storage account during deployment. This was happening during the file deployment stage of the CI/CD pipeline.

## Root Cause Analysis

### Initial Problem
The GitHub Actions service principal was missing storage roles, but the deeper issue was architectural:

1. **Storage Account Configuration**: The storage account had `allowSharedKeyAccess: false` when `enableAdvancedSecurity: true`
2. **Function App Configuration**: The Function App was still configured to use storage account keys via connection strings
3. **Security Mismatch**: Advanced security settings disabled key-based authentication, but the Function App wasn't configured for managed identity

### Why Key-Based Authentication Failed
```bicep
// In main.bicep - Storage account security settings
properties: {
  defaultToOAuthAuthentication: enableAdvancedSecurity  // true in dev
  allowSharedKeyAccess: !enableAdvancedSecurity        // false in dev - This blocked key access!
}
```

## Solution: Migrate to Managed Identity

### 1. Updated Function App Configuration
**Before (Key-based - FAILED):**
```bicep
appSettings: [
  {
    name: 'AzureWebJobsStorage'
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
  }
]
```

**After (Managed Identity - SUCCESS):**
```bicep
appSettings: [
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccount.name
  }
  {
    name: 'AzureWebJobsStorage__credential'
    value: 'managedidentity'
  }
]
```

### 2. Added Required Role Assignments
Added proper RBAC roles for Function App's system-assigned managed identity:

```bicep
// Storage Blob Data Contributor - for blob operations
resource functionAppStorageBlobAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage File Data SMB Share Contributor - for file share operations  
resource functionAppStorageFileAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0c867c2a-1d8c-454a-a3db-ab2ea1bdc8bb')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
```

### 3. Security Benefits of Managed Identity
- **No Shared Keys**: Eliminates storage account key management and rotation
- **Fine-grained Access**: RBAC provides precise permissions control
- **Azure AD Integration**: Leverages enterprise identity and access management
- **Automatic Token Management**: Azure handles authentication token lifecycle
- **Audit Trail**: All access is tracked through Azure AD and Azure Monitor

### 4. Operational Benefits
- **Zero Configuration Drift**: No connection strings to maintain or rotate
- **Environment Consistency**: Same authentication mechanism across all environments
- **Reduced Attack Surface**: No shared secrets that could be compromised
- **Simplified CI/CD**: No secret management in deployment pipelines

## Verification Steps

### 1. Validate Bicep Template
```bash
az bicep build --file infra/main.bicep
```

### 2. Test Deployment
```bash
az deployment group create \
  --resource-group rg-member-property-alert-dev-eastus2 \
  --template-file infra/main.bicep \
  --parameters @infra/main.dev.parameters.json
```

### 3. Verify Function App Identity
```bash
az functionapp identity show \
  --name func-mpa-dev-eus2-xxxx \
  --resource-group rg-member-property-alert-dev-eastus2
```

### 4. Verify Storage Role Assignments
```bash
az role assignment list \
  --scope "/subscriptions/{subscription-id}/resourceGroups/rg-member-property-alert-dev-eastus2/providers/Microsoft.Storage/storageAccounts/stmpadeveus26ih6" \
  --query "[].{PrincipalId:principalId, RoleDefinitionName:roleDefinitionName}"
```

## Migration Complete
- ✅ Updated Function App to use managed identity for storage access
- ✅ Added required RBAC role assignments in Bicep template  
- ✅ Maintained security best practices with advanced security enabled
- ✅ Eliminated dependency on storage account keys

## Next Steps
- Re-run deployment to verify the fix
- Monitor future deployments for any remaining permission issues
