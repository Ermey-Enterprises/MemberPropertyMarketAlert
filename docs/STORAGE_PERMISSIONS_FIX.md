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

**After (Managed Identity - COMPREHENSIVE):**
```bicep
appSettings: [
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccount.name
  }
  {
    name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING__accountName'
    value: storageAccount.name
  }
  {
    name: 'CosmosDb__Endpoint'  // Changed from connection string
    value: cosmosDbAccount.properties.documentEndpoint
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

// Cosmos DB Built-in Data Contributor - for Cosmos DB operations
resource functionAppCosmosDbAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: cosmosDbAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '00000000-0000-0000-0000-000000000002')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
```

### 3. Eliminated Secret Storage
**Before (Security Risk):**
```bicep
// Stored sensitive connection strings in Key Vault
resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value}'
  }
}
```

**After (Secure):**
```bicep
// No secrets stored - using managed identity endpoints only
// Commented out all connection string secrets
```

### 4. Security Benefits of Comprehensive Managed Identity
- **No Shared Keys**: Eliminates ALL account keys (storage, Cosmos DB)
- **Zero Secrets**: No connection strings stored in Key Vault or configuration
- **Fine-grained Access**: RBAC provides precise permissions for each service
- **Azure AD Integration**: Leverages enterprise identity across all Azure services
- **Automatic Token Management**: Azure handles authentication tokens for all services
- **Comprehensive Audit Trail**: All access tracked through Azure AD and Azure Monitor

### 5. Operational Benefits
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
