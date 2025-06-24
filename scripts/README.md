# Deployment Scripts

This directory contains scripts for deploying and managing the Member Property Alert infrastructure and applications.

## Scripts Overview

### Test-Deployment.ps1

PowerShell script for validating deployment configuration locally before actual deployment.

**Features:**
- Local Bicep template validation and compilation
- What-if analysis to preview deployment changes
- Parameter file validation
- Azure CLI connectivity testing
- Comprehensive pre-deployment checks

**Usage:**
```powershell
# Test dev environment deployment
.\Test-Deployment.ps1 -Environment dev

# Test with custom resource group
.\Test-Deployment.ps1 -Environment test -ResourceGroupName "my-custom-rg"

# Test specific location
.\Test-Deployment.ps1 -Environment prod -Location "westus2"
```

**Prerequisites:**
- Azure CLI installed and authenticated (`az login`)
- PowerShell 5.1 or PowerShell Core 7+
- Read permissions on target Azure subscription

### Deploy-Infrastructure.ps1

PowerShell script for deploying Azure infrastructure using Bicep templates.

> **Note**: Legacy Cosmos DB container creation scripts have been removed as all containers are now properly defined in the Bicep infrastructure template (`infra/main.bicep`).

**Features:**
- Environment-specific deployments (dev, test, prod)
- Validation and What-If analysis
- Comprehensive error handling and logging
- Parameter file support
- Resource group management

**Usage:**
```powershell
# Deploy to dev environment
.\Deploy-Infrastructure.ps1 -Environment dev

# Validate deployment without executing
.\Deploy-Infrastructure.ps1 -Environment prod -ValidateOnly

# Run What-If analysis
.\Deploy-Infrastructure.ps1 -Environment test -WhatIf

# Deploy to specific subscription and location
.\Deploy-Infrastructure.ps1 -Environment prod -SubscriptionId "your-sub-id" -Location "westus2"
```

**Prerequisites:**
- Azure CLI installed and authenticated (`az login`)
- PowerShell 5.1 or PowerShell Core 7+
- Appropriate Azure permissions (Contributor role)

**Environment Variables:**
- `RENTCAST_API_KEY`: RentCast API key for property data
- `ADMIN_API_KEY`: Admin API key for secure endpoints

## Manual Deployment Process

### 1. Infrastructure Deployment

```powershell
# Set environment variables
$env:RENTCAST_API_KEY = "your-rentcast-api-key"
$env:ADMIN_API_KEY = "your-admin-api-key"

# Deploy infrastructure
.\Deploy-Infrastructure.ps1 -Environment dev
```

### 2. Application Deployment

After infrastructure deployment, deploy applications:

#### Function App
```bash
# Build and publish Function App
dotnet publish ../src/MemberPropertyAlert.Functions/MemberPropertyAlert.Functions.csproj -c Release -o ./publish/functions

# Create deployment package
cd ./publish/functions
zip -r ../function-app.zip .
cd ../..

# Deploy to Azure
az functionapp deploy \
  --resource-group "rg-member-property-alert-dev-eastus2" \
  --name "func-member-property-alert-dev" \
  --src-path "./publish/function-app.zip" \
  --type zip
```

#### Web App
```bash
# Build React UI
cd ../src/MemberPropertyAlert.UI
npm ci
npm run build

# Create deployment package
cd build
zip -r ../../../scripts/publish/web-app.zip .
cd ../../../scripts

# Deploy to Azure
az webapp deploy \
  --resource-group "rg-member-property-alert-dev-eastus2" \
  --name "web-member-property-alert-dev" \
  --src-path "./publish/web-app.zip" \
  --type zip
```

### 3. Health Checks

Verify deployments:

```bash
# Test Function App
curl https://func-member-property-alert-dev.azurewebsites.net/api/health

# Test Web App
curl https://web-member-property-alert-dev.azurewebsites.net
```

## Troubleshooting

### Common Issues

#### 1. Authentication Errors
```
Error: Please run 'az login' to authenticate with Azure before using this command.
```
**Solution:** Run `az login` and authenticate with your Azure account.

#### 2. Permission Errors
```
Error: The client does not have authorization to perform action 'Microsoft.Resources/subscriptions/resourceGroups/write'
```
**Solution:** Ensure your account has Contributor role on the subscription or resource group.

#### 3. Resource Naming Conflicts
```
Error: The storage account name 'stmemberpropertyalertdev' is already taken.
```
**Solution:** The unique suffix should prevent this, but you may need to delete existing resources or use a different resource group.

#### 4. Bicep Compilation Errors
```
Error: Bicep template compilation failed
```
**Solution:** 
- Check Bicep syntax: `az bicep build --file ../infra/main.bicep`
- Validate parameter files are properly formatted JSON
- Ensure all required parameters are provided

### Debugging Steps

1. **Check Azure CLI Version:**
   ```bash
   az version
   ```

2. **Verify Authentication:**
   ```bash
   az account show
   ```

3. **Test Bicep Template:**
   ```bash
   az bicep build --file ../infra/main.bicep
   ```

4. **Validate Parameters:**
   ```bash
   az deployment group validate \
     --resource-group "rg-member-property-alert-dev-eastus2" \
     --template-file "../infra/main.bicep" \
     --parameters @../infra/main.dev.parameters.json
   ```

5. **Check Resource Group:**
   ```bash
   az group show --name "rg-member-property-alert-dev-eastus2"
   ```

## Environment-Specific Configurations

### Development (dev)
- **Resource Group:** `rg-member-property-alert-dev-eastus2`
- **App Service Plan:** B1 (Basic)
- **Cosmos DB:** Free Tier (if available) or Serverless
- **Backup Retention:** 7 days

### Test (test)
- **Resource Group:** `rg-member-property-alert-test-eastus2`
- **App Service Plan:** B1 (Basic)
- **Cosmos DB:** Serverless
- **Backup Retention:** 7 days

### Production (prod)
- **Resource Group:** `rg-member-property-alert-prod-eastus2`
- **App Service Plan:** P1v3 (Premium)
- **Cosmos DB:** Serverless
- **Backup Retention:** 30 days

## Security Best Practices

### Secrets Management
- Never commit API keys or secrets to source control
- Use environment variables for local development
- Use Azure Key Vault for production secrets
- Rotate secrets regularly

### Access Control
- Use service principals for automated deployments
- Apply principle of least privilege
- Regular access reviews
- Enable Azure AD authentication where possible

### Network Security
- HTTPS only for all web applications
- Consider private endpoints for production
- Implement proper CORS policies
- Use managed identities where possible

## Performance Optimization

### Build Performance
- Use NuGet package caching
- Leverage npm caching for Node.js builds
- Consider parallel builds for large solutions

### Deployment Performance
- Use async deployments where possible
- Implement health check retries
- Optimize artifact sizes
- Use deployment slots for zero-downtime deployments

## Monitoring and Logging

### Application Insights
All applications are configured with Application Insights:
- Function App: Automatic instrumentation
- Web App: Client-side telemetry
- Custom events for business logic

### Log Analysis
Query Application Insights for deployment and runtime issues:
```kusto
// Function App errors
traces
| where cloud_RoleName == "func-member-property-alert-dev"
| where severityLevel >= 3
| order by timestamp desc

// Web App performance
pageViews
| where cloud_RoleName == "web-member-property-alert-dev"
| summarize avg(duration) by bin(timestamp, 1h)
```

## Future Enhancements

### Planned Script Improvements
1. **Cross-platform support:** Add bash/shell script equivalents
2. **Configuration validation:** Pre-deployment configuration checks
3. **Rollback capabilities:** Automated rollback on deployment failures
4. **Cost estimation:** Pre-deployment cost analysis
5. **Resource tagging:** Automated resource tagging for cost tracking

### Automation Enhancements
1. **Infrastructure drift detection:** Monitor for manual changes
2. **Automated testing:** Post-deployment validation scripts
3. **Performance benchmarking:** Automated performance testing
4. **Security scanning:** Automated security vulnerability checks

## Support

For issues with deployment scripts:
1. Check the troubleshooting section above
2. Review Azure Activity Log for detailed error messages
3. Examine Application Insights for runtime issues
4. Contact the development team for assistance

## Contributing

When modifying deployment scripts:
1. Test changes in development environment first
2. Update documentation for any new parameters or features
3. Follow PowerShell best practices and error handling patterns
4. Add appropriate logging and user feedback
