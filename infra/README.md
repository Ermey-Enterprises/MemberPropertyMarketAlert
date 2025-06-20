# Azure Infrastructure Deployment

This directory contains the Bicep templates and parameter files for deploying the Member Property Market Alert service to Azure.

## 🏗️ Infrastructure Overview

The Bicep templates deploy a complete Azure infrastructure including:

### Core Services
- **Azure Functions**: .NET 8 isolated worker for the API backend
- **Azure Web App**: React-based admin dashboard
- **Cosmos DB**: Serverless NoSQL database for data storage
- **Application Insights**: Monitoring and telemetry
- **Storage Account**: Function app storage and file storage

### Environment-Specific Resources
- **Development**: Basic tier resources with free Cosmos DB tier
- **Test**: Standard tier resources for testing
- **Production**: Premium tier resources with enhanced backup and monitoring

## 📁 File Structure

```
infra/
├── main.bicep                    # Main Bicep template
├── main.dev.parameters.json     # Development environment parameters
├── main.test.parameters.json    # Test environment parameters
├── main.prod.parameters.json    # Production environment parameters
└── README.md                    # This file
```

## 🚀 Deployment Options

### Option 1: GitHub Actions (Recommended)

The infrastructure is automatically deployed via GitHub Actions workflows:

1. **Automatic Deployment**: Push to `main` branch deploys to production
2. **Manual Deployment**: Use the Infrastructure workflow for any environment
3. **Environment-Specific**: Each environment gets its own resource group

### Option 2: Azure CLI

Deploy manually using Azure CLI:

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "<your-subscription-id>"

# Create resource group
az group create \
  --name "MemberPropertyMarketAlert-dev-rg" \
  --location "East US"

# Deploy infrastructure
az deployment group create \
  --resource-group "MemberPropertyMarketAlert-dev-rg" \
  --template-file "./infra/main.bicep" \
  --parameters "./infra/main.dev.parameters.json" \
  --parameters rentCastApiKey="<your-rentcast-api-key>" \
  --parameters adminApiKey="<your-admin-api-key>"
```

### Option 3: PowerShell

Use the provided PowerShell deployment script:

```powershell
# Navigate to deploy directory
cd deploy

# Run deployment script
.\deploy.ps1 -Environment "dev" -SubscriptionId "<your-subscription-id>"
```

## 🔧 Configuration Parameters

### Required Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `environment` | Environment name (dev/test/prod) | `dev` |
| `location` | Azure region | `East US` |
| `appName` | Application name prefix | `member-property-alert` |
| `rentCastApiKey` | RentCast API key for property data | `your-api-key` |
| `adminApiKey` | Admin API key for secure endpoints | `secure-admin-key` |

### Environment-Specific Settings

#### Development Environment
- **Resource Group**: `MemberPropertyMarketAlert-dev-rg`
- **Cosmos DB**: Free tier enabled
- **App Service Plan**: Basic B1
- **Backup Retention**: 7 days
- **Log Retention**: 30 days

#### Test Environment
- **Resource Group**: `MemberPropertyMarketAlert-test-rg`
- **Cosmos DB**: Serverless (paid)
- **App Service Plan**: Basic B1
- **Backup Retention**: 7 days
- **Log Retention**: 30 days

#### Production Environment
- **Resource Group**: `MemberPropertyMarketAlert-prod-rg`
- **Cosmos DB**: Serverless (paid)
- **App Service Plan**: Premium P1v3
- **Backup Retention**: 30 days
- **Log Retention**: 90 days
- **Always On**: Enabled

## 🗄️ Database Schema

The Bicep template creates the following Cosmos DB containers:

### Institutions Container
- **Partition Key**: `/id`
- **Purpose**: Store financial institution information

### MemberAddresses Container
- **Partition Key**: `/institutionId`
- **Purpose**: Store member property addresses for monitoring

### PropertyAlerts Container
- **Partition Key**: `/institutionId`
- **Purpose**: Store alerts when properties are listed for sale

### ScanLogs Container
- **Partition Key**: `/date`
- **Purpose**: Store scan execution logs and statistics

## 🔐 Security Configuration

### Application Settings
The Bicep template automatically configures:
- **Cosmos DB Connection**: Secure connection string
- **Application Insights**: Telemetry and monitoring
- **RentCast API**: Property data service integration
- **Admin API Key**: Secure endpoint authentication

### Network Security
- **HTTPS Only**: All web traffic encrypted
- **CORS**: Configured for admin UI access
- **Storage**: Blob public access disabled
- **TLS**: Minimum version 1.2 enforced

### Identity and Access
- **Managed Identity**: System-assigned for Function App
- **Key Vault Integration**: Ready for secret management
- **RBAC**: Role-based access control enabled

## 📊 Monitoring and Logging

### Application Insights
- **Performance Monitoring**: Request/response times
- **Error Tracking**: Exception logging and alerting
- **Custom Metrics**: Business-specific telemetry
- **Live Metrics**: Real-time application monitoring

### Log Analytics
- **Centralized Logging**: All application and infrastructure logs
- **Query Capabilities**: KQL queries for analysis
- **Alerting**: Custom alerts based on log patterns
- **Retention**: Environment-specific retention policies

## 💰 Cost Optimization

### Development Environment
- **Cosmos DB**: Free tier (1000 RU/s, 25GB storage)
- **App Service**: Basic B1 (~$13/month)
- **Storage**: Standard LRS (~$2/month)
- **Application Insights**: Free tier (1GB/month)
- **Total Estimated Cost**: ~$15/month

### Production Environment
- **Cosmos DB**: Serverless (pay-per-use)
- **App Service**: Premium P1v3 (~$146/month)
- **Storage**: Standard LRS (~$5/month)
- **Application Insights**: Pay-per-GB (~$2.30/GB)
- **Total Estimated Cost**: ~$160-200/month

## 🔄 Deployment Outputs

After successful deployment, the template outputs:

| Output | Description | Example |
|--------|-------------|---------|
| `functionAppName` | Azure Function app name | `func-member-property-alert-dev` |
| `functionAppUrl` | Function app URL | `https://func-member-property-alert-dev.azurewebsites.net` |
| `webAppName` | Web app name | `web-member-property-alert-dev` |
| `webAppUrl` | Web app URL | `https://web-member-property-alert-dev.azurewebsites.net` |
| `cosmosAccountName` | Cosmos DB account name | `cosmos-member-property-alert-dev` |
| `storageAccountName` | Storage account name | `stmemberpropertyalertdev123456` |

## 🚨 Troubleshooting

### Common Issues

#### 1. Deployment Fails with "Resource Already Exists"
```bash
# Delete existing resource group
az group delete --name "MemberPropertyMarketAlert-dev-rg" --yes

# Redeploy
az deployment group create --resource-group "MemberPropertyMarketAlert-dev-rg" ...
```

#### 2. Cosmos DB Free Tier Limit Exceeded
- Only one free tier Cosmos DB account per subscription
- Use paid tier for additional environments
- Consider using different subscriptions for dev/test/prod

#### 3. Function App Cold Start Issues
- Enable "Always On" for production environments
- Consider Premium plan for better performance
- Implement health check endpoints

#### 4. Storage Account Name Conflicts
- Storage account names must be globally unique
- The template uses a unique suffix to avoid conflicts
- If deployment fails, try a different resource group name

### Validation Commands

```bash
# Validate Bicep template
az deployment group validate \
  --resource-group "MemberPropertyMarketAlert-dev-rg" \
  --template-file "./infra/main.bicep" \
  --parameters "./infra/main.dev.parameters.json"

# What-if deployment (preview changes)
az deployment group what-if \
  --resource-group "MemberPropertyMarketAlert-dev-rg" \
  --template-file "./infra/main.bicep" \
  --parameters "./infra/main.dev.parameters.json"
```

## 📚 Additional Resources

- [Azure Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)
- [Cosmos DB Documentation](https://docs.microsoft.com/en-us/azure/cosmos-db/)
- [Application Insights Documentation](https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview)

## 🤝 Support

For deployment issues:
1. Check the [Azure CI/CD Setup Guide](../docs/AZURE_CICD_SETUP.md)
2. Review GitHub Actions logs
3. Validate Bicep templates locally
4. Check Azure portal for resource status
