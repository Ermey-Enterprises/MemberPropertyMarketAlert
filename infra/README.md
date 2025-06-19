# Infrastructure Overview

This directory contains the Azure infrastructure definition for the Member Property Market Alert service using Azure Bicep templates.

## 🏗️ Architecture

The infrastructure deploys the following Azure resources:

### Core Services
- **Azure Functions**: Background processing and API endpoints
- **App Service**: Web application hosting
- **Cosmos DB**: NoSQL database for application data
- **Service Bus**: Message queuing for asynchronous processing
- **Application Insights**: Application monitoring and logging

### Supporting Services
- **Azure Container Registry**: Container image storage
- **Key Vault**: Secrets and configuration management
- **Storage Account**: Function app storage and blob storage
- **Log Analytics**: Centralized logging

## 📁 File Structure

```
infra/
├── main.bicep                    # Main Bicep template
├── main.dev.parameters.json     # Development environment parameters
├── main.test.parameters.json    # Test environment parameters
└── main.prod.parameters.json    # Production environment parameters
```

## 🚀 Quick Start

### Prerequisites

1. **Azure CLI**: Install from [here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
2. **Bicep CLI**: Install using `az bicep install`
3. **Azure Subscription**: With Contributor access

### Local Deployment

```powershell
# Login to Azure
az login

# Test the deployment (no actual resources created)
.\test-deployment.ps1 -Environment dev

# Preview what will be deployed
.\deploy-bicep.ps1 -Environment dev -WhatIf

# Deploy to development environment
.\deploy-bicep.ps1 -Environment dev
```

## 🌍 Environments

The infrastructure supports three environments:

### Development (`dev`)
- **Purpose**: Development and testing
- **Resources**: Serverless/consumption-based pricing
- **Data retention**: 30 days
- **Backup**: Local redundancy

### Test (`test`)
- **Purpose**: Pre-production testing
- **Resources**: Basic/standard tiers
- **Data retention**: 90 days
- **Backup**: Local redundancy

### Production (`prod`)
- **Purpose**: Live production workload
- **Resources**: Premium tiers with high availability
- **Data retention**: 365 days
- **Backup**: Geo-redundant storage

## 📊 Resource Naming Convention

Resources follow this naming pattern:
- **Pattern**: `{baseName}-{environment}-{resourceType}`
- **Example**: `memberpropertyalert-prod-func`

| Resource Type | Naming Example |
|---------------|----------------|
| Resource Group | `MemberPropertyMarketAlert-rg` |
| Function App | `memberpropertyalert-dev-func` |
| Web App | `memberpropertyalert-dev-web` |
| Cosmos DB | `memberpropertyalert-dev-cosmos` |
| Service Bus | `memberpropertyalert-dev-sb` |
| Storage Account | `memberpropertyalertdevstorage` |
| Key Vault | `memberpropertyalert-dev-kv` |

## ⚙️ Configuration

### Parameters

Key parameters that can be customized:

| Parameter | Description | Default |
|-----------|-------------|---------|
| `environment` | Target environment | `dev` |
| `location` | Azure region | Resource group location |
| `baseName` | Base name for resources | `memberpropertyalert` |
| `adminEmail` | Administrator email | Required |

### Tags

All resources are tagged with:
- `Environment`: The deployment environment
- `Project`: `MemberPropertyMarketAlert`
- `ManagedBy`: `Bicep`

## 🔒 Security Features

### Network Security
- **HTTPS Only**: All web services enforce HTTPS
- **TLS 1.2**: Minimum TLS version enforced
- **Private Endpoints**: Available for Cosmos DB and Storage (prod)

### Identity and Access
- **Managed Identity**: System-assigned for all compute services
- **RBAC**: Role-based access control
- **Key Vault**: Centralized secrets management

### Data Protection
- **Encryption at Rest**: All storage services
- **Encryption in Transit**: TLS/HTTPS for all communications
- **Backup**: Automated backup with retention policies

## 💰 Cost Optimization

### Development Environment
- Serverless Azure Functions (pay-per-execution)
- Cosmos DB serverless (pay-per-request)
- Basic App Service Plan

### Production Environment
- Reserved capacity for predictable workloads
- Auto-scaling enabled
- Geo-redundant storage for critical data

### Cost Monitoring
- Budget alerts configured
- Resource tagging for cost allocation
- Regular cost reviews recommended

## 📈 Monitoring and Alerting

### Application Insights
- **Performance monitoring**: Response times, throughput
- **Error tracking**: Exceptions and failed requests
- **Custom metrics**: Business-specific KPIs

### Log Analytics
- **Centralized logging**: All service logs
- **Query capabilities**: KQL for advanced analysis
- **Retention**: Environment-specific retention policies

### Recommended Alerts
- Function execution failures
- High response times
- Cosmos DB throttling
- Storage account errors

## 🔧 Maintenance

### Regular Tasks
1. **Review costs**: Monthly cost analysis
2. **Update dependencies**: Quarterly updates
3. **Security patches**: As needed
4. **Backup testing**: Quarterly restore tests

### Scaling Considerations
- **Function App**: Automatic scaling based on demand
- **Cosmos DB**: Manual throughput adjustment
- **App Service**: Scale up/out based on metrics

## 🆘 Troubleshooting

### Common Issues

#### Deployment Failures
```bash
# Check deployment status
az deployment group show --name {deployment-name} --resource-group MemberPropertyMarketAlert-rg

# Validate template
az deployment group validate --template-file main.bicep --parameters main.dev.parameters.json
```

#### Resource Access Issues
```bash
# Check role assignments
az role assignment list --scope /subscriptions/{subscription-id}/resourceGroups/MemberPropertyMarketAlert-rg

# Verify managed identity
az webapp identity show --name {web-app-name} --resource-group MemberPropertyMarketAlert-rg
```

#### Connectivity Issues
```bash
# Test Function App
curl https://{function-app-name}.azurewebsites.net/api/health

# Test Web App
curl https://{web-app-name}.azurewebsites.net/health
```

### Support Resources
- [Azure Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)
- [Cosmos DB Documentation](https://docs.microsoft.com/en-us/azure/cosmos-db/)

## 🚀 Next Steps

After successful deployment:

1. **Configure Application Settings**: Update connection strings and API keys
2. **Deploy Application Code**: Use CI/CD pipeline or manual deployment
3. **Set up Monitoring**: Configure alerts and dashboards
4. **Test Functionality**: Verify all endpoints are working
5. **Documentation**: Update any environment-specific documentation

---

For questions or issues with the infrastructure, please refer to the troubleshooting section or create an issue in the repository.
