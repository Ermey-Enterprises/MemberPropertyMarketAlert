# Member Property Market Alert - Deployment Guide

## Overview

This guide covers the deployment of the Member Property Market Alert service to Azure, including all required infrastructure components.

## Prerequisites

- Azure subscription with appropriate permissions
- Azure CLI installed and configured
- .NET 8 SDK installed
- Visual Studio 2022 or VS Code
- Git for source control

## Azure Resources Required

### Core Services
1. **Azure Functions App** - Hosts the serverless functions
2. **Azure Cosmos DB** - NoSQL database for storing data
3. **Azure Service Bus** - Message queuing for reliable processing
4. **Azure Key Vault** - Secure storage for secrets and API keys
5. **Application Insights** - Monitoring and telemetry
6. **Azure Storage Account** - Required for Azure Functions

### Optional Services
1. **Azure API Management** - API gateway and management
2. **Azure Logic Apps** - Workflow automation
3. **Azure Event Grid** - Event-driven architecture

## Deployment Steps

### 1. Infrastructure Setup

#### Create Resource Group
```bash
az group create --name rg-member-property-alert --location eastus
```

#### Create Storage Account
```bash
az storage account create \
  --name stmemberpropertyalert \
  --resource-group rg-member-property-alert \
  --location eastus \
  --sku Standard_LRS
```

#### Create Cosmos DB Account
```bash
az cosmosdb create \
  --name cosmos-member-property-alert \
  --resource-group rg-member-property-alert \
  --locations regionName=eastus \
  --default-consistency-level Session
```

#### Create Cosmos DB Database and Containers
```bash
# Create database
az cosmosdb sql database create \
  --account-name cosmos-member-property-alert \
  --resource-group rg-member-property-alert \
  --name MemberPropertyMarketAlert

# Create MemberAddresses container
az cosmosdb sql container create \
  --account-name cosmos-member-property-alert \
  --resource-group rg-member-property-alert \
  --database-name MemberPropertyMarketAlert \
  --name MemberAddresses \
  --partition-key-path "/partitionKey" \
  --throughput 400

# Create PropertyListings container
az cosmosdb sql container create \
  --account-name cosmos-member-property-alert \
  --resource-group rg-member-property-alert \
  --database-name MemberPropertyMarketAlert \
  --name PropertyListings \
  --partition-key-path "/partitionKey" \
  --throughput 400

# Create PropertyMatches container
az cosmosdb sql container create \
  --account-name cosmos-member-property-alert \
  --resource-group rg-member-property-alert \
  --database-name MemberPropertyMarketAlert \
  --name PropertyMatches \
  --partition-key-path "/partitionKey" \
  --throughput 400
```

#### Create Service Bus Namespace
```bash
az servicebus namespace create \
  --name sb-member-property-alert \
  --resource-group rg-member-property-alert \
  --location eastus \
  --sku Standard

# Create queue for property listings
az servicebus queue create \
  --namespace-name sb-member-property-alert \
  --resource-group rg-member-property-alert \
  --name property-listings
```

#### Create Key Vault
```bash
az keyvault create \
  --name kv-member-property-alert \
  --resource-group rg-member-property-alert \
  --location eastus
```

#### Create Application Insights
```bash
az monitor app-insights component create \
  --app ai-member-property-alert \
  --location eastus \
  --resource-group rg-member-property-alert
```

#### Create Function App
```bash
az functionapp create \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --storage-account stmemberpropertyalert \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4
```

### 2. Configuration

#### Get Connection Strings
```bash
# Cosmos DB connection string
az cosmosdb keys list \
  --name cosmos-member-property-alert \
  --resource-group rg-member-property-alert \
  --type connection-strings

# Service Bus connection string
az servicebus namespace authorization-rule keys list \
  --namespace-name sb-member-property-alert \
  --resource-group rg-member-property-alert \
  --name RootManageSharedAccessKey

# Application Insights connection string
az monitor app-insights component show \
  --app ai-member-property-alert \
  --resource-group rg-member-property-alert
```

#### Store Secrets in Key Vault
```bash
# Store Cosmos DB connection string
az keyvault secret set \
  --vault-name kv-member-property-alert \
  --name "CosmosDb-ConnectionString" \
  --value "YOUR_COSMOS_CONNECTION_STRING"

# Store Service Bus connection string
az keyvault secret set \
  --vault-name kv-member-property-alert \
  --name "ServiceBus-ConnectionString" \
  --value "YOUR_SERVICEBUS_CONNECTION_STRING"

# Store Application Insights connection string
az keyvault secret set \
  --vault-name kv-member-property-alert \
  --name "ApplicationInsights-ConnectionString" \
  --value "YOUR_APPINSIGHTS_CONNECTION_STRING"
```

#### Configure Function App Settings
```bash
# Set Key Vault reference for Cosmos DB
az functionapp config appsettings set \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --settings "ConnectionStrings__CosmosDb=@Microsoft.KeyVault(VaultName=kv-member-property-alert;SecretName=CosmosDb-ConnectionString)"

# Set Key Vault reference for Service Bus
az functionapp config appsettings set \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --settings "ServiceBusConnection=@Microsoft.KeyVault(VaultName=kv-member-property-alert;SecretName=ServiceBus-ConnectionString)"

# Set Application Insights
az functionapp config appsettings set \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=@Microsoft.KeyVault(VaultName=kv-member-property-alert;SecretName=ApplicationInsights-ConnectionString)"

# Set other configuration
az functionapp config appsettings set \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --settings "CosmosDb__DatabaseName=MemberPropertyMarketAlert"
```

#### Grant Function App Access to Key Vault
```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert

# Get the principal ID
PRINCIPAL_ID=$(az functionapp identity show \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --query principalId -o tsv)

# Grant access to Key Vault
az keyvault set-policy \
  --name kv-member-property-alert \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

### 3. Application Deployment

#### Build and Publish
```bash
# Navigate to the Functions project
cd src/MemberPropertyMarketAlert.Functions

# Build the project
dotnet build --configuration Release

# Publish to Azure
func azure functionapp publish func-member-property-alert
```

#### Alternative: Deploy from Visual Studio
1. Right-click on the Functions project
2. Select "Publish"
3. Choose "Azure Functions Consumption Plan"
4. Select your subscription and function app
5. Click "Publish"

### 4. Verification

#### Test API Endpoints
```bash
# Get function URL
FUNCTION_URL=$(az functionapp show \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --query defaultHostName -o tsv)

# Test health endpoint (if implemented)
curl "https://$FUNCTION_URL/api/health"
```

#### Monitor Logs
```bash
# Stream logs
az functionapp log tail \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert
```

## Environment-Specific Configurations

### Development Environment
- Use Azure Storage Emulator
- Use Cosmos DB Emulator
- Local settings in `local.settings.json`

### Staging Environment
- Separate resource group: `rg-member-property-alert-staging`
- Lower-tier services for cost optimization
- Separate Key Vault for staging secrets

### Production Environment
- High availability configuration
- Auto-scaling enabled
- Backup and disaster recovery
- Enhanced monitoring and alerting

## Security Considerations

### Network Security
```bash
# Configure Function App to use VNet integration (if required)
az functionapp vnet-integration add \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --vnet MyVNet \
  --subnet MySubnet
```

### Access Control
```bash
# Configure authentication (if required)
az functionapp auth update \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --enabled true \
  --action LoginWithAzureActiveDirectory
```

## Monitoring and Alerting

### Application Insights Queries
```kusto
// Function execution times
requests
| where cloud_RoleName == "func-member-property-alert"
| summarize avg(duration), count() by name
| order by avg_duration desc

// Error rates
requests
| where cloud_RoleName == "func-member-property-alert"
| summarize ErrorRate = countif(success == false) * 100.0 / count() by bin(timestamp, 5m)
| render timechart
```

### Alerts
```bash
# Create alert for high error rate
az monitor metrics alert create \
  --name "High Error Rate" \
  --resource-group rg-member-property-alert \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/rg-member-property-alert/providers/Microsoft.Web/sites/func-member-property-alert" \
  --condition "avg requests/failed > 5" \
  --window-size 5m \
  --evaluation-frequency 1m
```

## Backup and Disaster Recovery

### Cosmos DB Backup
- Automatic backups are enabled by default
- Configure backup retention period as needed
- Consider geo-redundancy for critical data

### Function App Backup
```bash
# Enable backup (requires App Service Plan)
az functionapp config backup update \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --storage-account-url "https://stmemberpropertyalert.blob.core.windows.net/backups" \
  --frequency-interval 1 \
  --frequency-unit Day \
  --retention-period-in-days 30
```

## Cost Optimization

### Recommendations
1. Use consumption plan for Functions (pay-per-execution)
2. Configure Cosmos DB autoscale
3. Use reserved capacity for predictable workloads
4. Monitor and optimize resource usage
5. Implement proper lifecycle policies for storage

### Cost Monitoring
```bash
# Set up budget alerts
az consumption budget create \
  --budget-name "MemberPropertyAlert-Budget" \
  --amount 100 \
  --time-grain Monthly \
  --time-period start-date=2025-01-01 \
  --resource-group rg-member-property-alert
```

## Troubleshooting

### Common Issues
1. **Function not starting**: Check application settings and dependencies
2. **Cosmos DB connection issues**: Verify connection string and firewall rules
3. **Key Vault access denied**: Check managed identity permissions
4. **High latency**: Review Cosmos DB RU consumption and indexing

### Diagnostic Commands
```bash
# Check function app status
az functionapp show \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --query state

# View recent deployments
az functionapp deployment list \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert

# Check application settings
az functionapp config appsettings list \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert
```

## Maintenance

### Regular Tasks
1. Monitor resource usage and costs
2. Update dependencies and security patches
3. Review and rotate secrets
4. Backup verification
5. Performance optimization
6. Security audits

### Update Deployment
```bash
# Update function app
func azure functionapp publish func-member-property-alert --force

# Update application settings if needed
az functionapp config appsettings set \
  --name func-member-property-alert \
  --resource-group rg-member-property-alert \
  --settings "NewSetting=NewValue"
