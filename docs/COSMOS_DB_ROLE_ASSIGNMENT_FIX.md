# Cosmos DB Role Assignment Fix

## Problem Resolved

Fixed the Azure deployment error: "The specified role definition with ID '00000000-0000-0000-0000-000000000002' does not exist."

## Updates

### **Latest Fix (Scope Property)**
Added the missing `scope` property to the Cosmos DB role assignment:

```bicep
resource functionAppCosmosDbAccess 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = if (deployFunctionApp) {
  parent: cosmosDbAccount
  name: guid(cosmosDbAccount.id, resourceNames.functionApp, 'Cosmos DB Built-in Data Contributor')
  properties: {
    roleDefinitionId: '${cosmosDbAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: deployFunctionApp ? functionApp.identity.principalId : ''
    scope: cosmosDbAccount.id  // Added this required property
  }
}
```

The `scope` property is required for Cosmos DB role assignments and should be set to the Cosmos DB account resource ID.

## Root Cause

The Bicep template was using the generic Azure RBAC role assignment resource type for Cosmos DB data plane access, but Cosmos DB has its own specialized role assignment system:

**Problematic Configuration:**
```bicep
resource functionAppCosmosDbAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: cosmosDbAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '00000000-0000-0000-0000-000000000002')
  }
}
```

**The Issue:**
- Cosmos DB data plane roles are NOT part of Azure RBAC (Microsoft.Authorization)
- Cosmos DB uses its own role system: `Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments`
- The role definition ID `00000000-0000-0000-0000-000000000002` doesn't exist in Azure RBAC
- Cosmos DB Built-in Data Contributor is a Cosmos DB-specific role, not an Azure role

## Solution Implemented

### **Correct Cosmos DB Role Assignment**
Use the Cosmos DB-specific resource type and role definition format:

```bicep
resource functionAppCosmosDbAccess 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosDbAccount
  name: guid(cosmosDbAccount.id, resourceNames.functionApp, 'Cosmos DB Built-in Data Contributor')
  properties: {
    roleDefinitionId: '${cosmosDbAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: functionApp.identity.principalId
  }
}
```

## Key Differences

### **Azure RBAC vs Cosmos DB Roles**

| Aspect | Azure RBAC | Cosmos DB Roles |
|--------|------------|-----------------|
| **Resource Type** | `Microsoft.Authorization/roleAssignments` | `Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments` |
| **Scope** | Azure resource management | Cosmos DB data plane operations |
| **Role Definition Format** | `/subscriptions/{sub}/providers/Microsoft.Authorization/roleDefinitions/{id}` | `{cosmosAccount}/sqlRoleDefinitions/{id}` |
| **Built-in Roles** | Azure subscription-level | Cosmos DB account-level |

### **Role Definition IDs**

**Cosmos DB Built-in Roles:**
- **Data Reader**: `00000000-0000-0000-0000-000000000001`
- **Data Contributor**: `00000000-0000-0000-0000-000000000002`

These IDs are **only valid** within the Cosmos DB account context, not as Azure RBAC roles.

## Benefits of Correct Implementation

### ✅ **Proper Data Plane Access**
- Function App can read, write, and query Cosmos DB data
- Uses Cosmos DB's native authentication system
- Supports fine-grained database and container permissions

### ✅ **Security Best Practices**
- No connection strings needed for data access
- Managed identity authentication to Cosmos DB
- Principle of least privilege with data-specific roles

### ✅ **Operational Excellence**
- Follows Cosmos DB recommended practices
- Compatible with Cosmos DB SDKs and authentication
- Enables seamless scaling and management

## Verification Steps

### **1. Validate Role Assignment**
```bash
az cosmosdb sql role assignment list \
  --account-name cosmos-mpa-dev-eus2-xxxx \
  --resource-group rg-member-property-alert-dev-eastus2
```

### **2. Test Application Access**
```bash
# Function App should be able to access Cosmos DB using managed identity
curl https://func-mpa-dev-eus2-xxxx.azurewebsites.net/api/health
```

### **3. Check Role Definition**
```bash
az cosmosdb sql role definition show \
  --account-name cosmos-mpa-dev-eus2-xxxx \
  --resource-group rg-member-property-alert-dev-eastus2 \
  --role-definition-id "00000000-0000-0000-0000-000000000002"
```

## Application Configuration

### **Function App Settings**
The Function App is configured with both connection string and managed identity options:

```bicep
appSettings: [
  {
    name: 'CosmosDb__ConnectionString'
    value: cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
  {
    name: 'CosmosDb__Endpoint'
    value: cosmosDbAccount.properties.documentEndpoint
  }
]
```

### **Application Code Usage**
The application can choose the authentication method:

**Option 1: Connection String (Traditional)**
```csharp
var cosmosClient = new CosmosClient(connectionString);
```

**Option 2: Managed Identity (Recommended)**
```csharp
var credential = new DefaultAzureCredential();
var cosmosClient = new CosmosClient(endpoint, credential);
```

## Troubleshooting

### **Common Issues**

**Issue**: "Role assignment fails with access denied"
- **Cause**: Deployment service principal lacks Cosmos DB management permissions
- **Solution**: Ensure deployment principal has "Cosmos DB Operator" role

**Issue**: "Application can't access Cosmos DB with managed identity"
- **Cause**: Role assignment not propagated or incorrect endpoint
- **Solution**: Check role assignment exists and use correct endpoint URL

**Issue**: "Role definition not found in different environment"
- **Cause**: Using wrong Cosmos DB account reference
- **Solution**: Ensure role definition ID includes correct account scope

## Best Practices

### **Cosmos DB Role Management**
1. **Use Built-in Roles**: Prefer built-in Data Reader/Contributor over custom roles
2. **Scope Appropriately**: Assign roles at database or container level if needed
3. **Monitor Access**: Enable Cosmos DB audit logging for access tracking
4. **Regular Review**: Periodically review role assignments for compliance

### **Deployment Strategy**
1. **Account First**: Deploy Cosmos DB account before role assignments
2. **Identity Dependencies**: Ensure managed identities exist before role assignments
3. **Validation**: Test role assignments after deployment
4. **Documentation**: Keep role assignment documentation current

## Summary

This fix resolves the Cosmos DB role assignment issue by:

- **Using correct resource type**: `Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments`
- **Proper role definition format**: Account-scoped role definition ID
- **Following Cosmos DB best practices**: Data plane role assignments for managed identity
- **Enabling secure data access**: No connection strings needed for application access

The deployment should now complete successfully with proper Cosmos DB managed identity access configured.
