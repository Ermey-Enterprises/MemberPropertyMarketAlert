# Infrastructure Migration Guide

## Overview

This guide covers the migration from the legacy Bicep template to the new modernized infrastructure that follows Microsoft Cloud Adoption Framework (CAF) best practices.

## 🚀 What's New

### Key Improvements

1. **Enhanced Security**
   - **Azure Key Vault Integration**: All secrets now stored securely in Key Vault
   - **Managed Identity**: System-assigned managed identities for secure authentication
   - **RBAC**: Role-based access control for Key Vault access
   - **Advanced Security Features**: Configurable security enhancements

2. **Modern Resource Naming**
   - **Microsoft CAF Compliance**: Follows Azure naming conventions
   - **Consistent Patterns**: Standardized resource naming across all environments
   - **Location Abbreviations**: Efficient location codes (eastus2 → eus2)

3. **Environment-Driven Configuration**
   - **Conditional Deployments**: Deploy only what you need
   - **Environment-Specific Sizing**: Optimized resource allocation per environment
   - **Advanced Backup Policies**: Environment-appropriate retention settings

4. **Enhanced Monitoring**
   - **Log Analytics Integration**: Centralized logging with Application Insights
   - **Comprehensive Tagging**: Better cost management and resource organization
   - **Health Check Endpoints**: Built-in monitoring capabilities

## 📋 Migration Steps

### Step 1: Update GitHub Secrets

The new infrastructure requires the same secrets but uses them more securely:

**Required Secrets (no changes needed):**
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID` 
- `AZURE_SUBSCRIPTION_ID`
- `RENTCAST_API_KEY`
- `ADMIN_API_KEY`

### Step 2: Resource Naming Changes

**Old Naming Pattern:**
```
func-member-property-alert-dev
web-member-property-alert-dev
cosmos-member-property-alert-dev
```

**New Naming Pattern:**
```
func-mpa-dev-eus2-{uniqueSuffix}
web-mpa-dev-eus2-{uniqueSuffix}
cosmos-mpa-dev-eus2-{uniqueSuffix}
kv-mpa-dev-eus2-{uniqueSuffix}
```

### Step 3: Key Vault Integration

**Before (App Settings):**
```json
{
  "CosmosDb__ConnectionString": "AccountEndpoint=https://...",
  "RentCast__ApiKey": "your-api-key",
  "AdminApiKey": "your-admin-key"
}
```

**After (Key Vault References):**
```json
{
  "CosmosDb__ConnectionString": "@Microsoft.KeyVault(VaultName=kv-mpa-dev-eus2-xxxx;SecretName=COSMOS-CONNECTION-STRING)",
  "RentCast__ApiKey": "@Microsoft.KeyVault(VaultName=kv-mpa-dev-eus2-xxxx;SecretName=RENTCAST-API-KEY)",
  "AdminApiKey": "@Microsoft.KeyVault(VaultName=kv-mpa-dev-eus2-xxxx;SecretName=ADMIN-API-KEY)"
}
```

### Step 4: Deploy New Infrastructure

1. **Backup Current Environment** (if needed)
   ```bash
   # Export current app settings
   az functionapp config appsettings list --name func-member-property-alert-dev --resource-group rg-member-property-alert-dev-eastus2 > backup-settings.json
   ```

2. **Deploy New Infrastructure**
   ```bash
   # The GitHub Actions workflow will handle this automatically
   # Or deploy manually:
   az deployment group create \
     --resource-group rg-member-property-alert-dev-eastus2 \
     --template-file infra/main.bicep \
     --parameters @infra/main.dev.parameters.json \
       rentCastApiKey="your-api-key" \
       adminApiKey="your-admin-key"
   ```

3. **Verify Deployment**
   - Check that Key Vault is created and populated
   - Verify App Services have managed identities
   - Confirm RBAC assignments are in place

## 🔧 Configuration Changes

### Environment-Specific Features

**Development Environment:**
- Basic tier App Service Plan
- Cosmos DB free tier enabled
- 30-day log retention
- Advanced security disabled for easier development

**Test Environment:**
- Standard tier App Service Plan
- Advanced security enabled
- 60-day log retention
- Zone redundancy disabled

**Production Environment:**
- Premium tier App Service Plan
- Advanced security enabled
- 90-day log retention
- Zone redundancy enabled
- Enhanced backup policies

### New Parameters

The new Bicep template includes additional parameters:

```json
{
  "deployFunctionApp": true,      // Control Function App deployment
  "deployWebApp": true,           // Control Web App deployment
  "enableAdvancedSecurity": true  // Enable enhanced security features
}
```

## 🔐 Security Enhancements

### Key Vault Secrets

All secrets are now stored in Key Vault with proper naming:

| Secret Name | Description |
|-------------|-------------|
| `RENTCAST-API-KEY` | RentCast API key for property data |
| `ADMIN-API-KEY` | Admin API key for secure endpoints |
| `COSMOS-CONNECTION-STRING` | Cosmos DB connection string |
| `STORAGE-CONNECTION-STRING` | Storage account connection string |
| `APPLICATION-INSIGHTS-CONNECTION-STRING` | Application Insights connection |

### Managed Identity

Both Function App and Web App now use system-assigned managed identities:

- **No more connection strings in app settings**
- **Automatic Key Vault access via RBAC**
- **Enhanced security posture**

### RBAC Assignments

Automatic role assignments:
- Function App → Key Vault Secrets User
- Web App → Key Vault Secrets User

## 📊 Monitoring & Logging

### Enhanced Application Insights

- **Log Analytics Integration**: Centralized logging
- **Custom Dashboards**: Environment-specific monitoring
- **Alert Rules**: Proactive monitoring (can be added)

### Resource Tagging

Comprehensive tagging strategy:
```json
{
  "Environment": "dev",
  "Application": "MemberPropertyAlert",
  "Workload": "mpa",
  "ManagedBy": "Bicep",
  "CostCenter": "IT",
  "Owner": "DevOps",
  "Project": "PropertyMarketAlert"
}
```

## 🚨 Breaking Changes

### Application Code Changes Required

**None!** The application code remains unchanged. All changes are at the infrastructure level.

### Configuration Changes

1. **App Settings**: Now use Key Vault references (handled automatically)
2. **Health Endpoints**: Ensure `/api/health` endpoint exists for Function App
3. **CORS Settings**: Automatically configured for cross-origin requests

## 🔄 Rollback Plan

If you need to rollback to the previous infrastructure:

1. **Keep the old resource group** until migration is verified
2. **Update DNS/routing** to point back to old resources
3. **Restore app settings** from backup if needed

## ✅ Verification Checklist

After migration, verify:

- [ ] Function App is accessible and healthy
- [ ] Web App is accessible and healthy  
- [ ] Key Vault contains all required secrets
- [ ] Managed identities are assigned
- [ ] RBAC permissions are configured
- [ ] Application Insights is receiving telemetry
- [ ] Cosmos DB is accessible from applications
- [ ] All environment-specific configurations are correct

## 🆘 Troubleshooting

### Common Issues

1. **Key Vault Access Denied**
   - Verify managed identity is assigned
   - Check RBAC role assignments
   - Ensure Key Vault RBAC is enabled

2. **Application Not Starting**
   - Check Key Vault references in app settings
   - Verify secrets exist in Key Vault
   - Review Application Insights logs

3. **Deployment Failures**
   - Validate Bicep template syntax
   - Check parameter values
   - Verify Azure permissions

### Support

For issues with the new infrastructure:
1. Check Application Insights logs
2. Review Azure Activity Log
3. Validate Key Vault access logs
4. Check GitHub Actions workflow logs

## 📚 Additional Resources

- [Microsoft Cloud Adoption Framework](https://docs.microsoft.com/en-us/azure/cloud-adoption-framework/)
- [Azure Key Vault Best Practices](https://docs.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [Azure Managed Identity Documentation](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/)
- [Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
