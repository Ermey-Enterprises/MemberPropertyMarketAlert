# CI/CD Setup Guide for Member Property Alert

This document provides a comprehensive guide for setting up and configuring the CI/CD pipeline for the Member Property Alert project, adapted from the CallAssistant project's proven deployment patterns.

## Overview

The CI/CD pipeline is implemented using GitHub Actions and follows these key principles:
- **Azure OIDC Authentication** for secure, keyless authentication
- **Change Detection** for optimized deployments
- **Environment-specific Deployments** (dev, test, prod)
- **Infrastructure as Code** using Azure Bicep
- **Comprehensive Testing** and health checks

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   GitHub Repo   │───▶│  GitHub Actions  │───▶│  Azure Resources │
│                 │    │                  │    │                 │
│ • Source Code   │    │ • Build & Test   │    │ • Function App  │
│ • Bicep Templates│    │ • Deploy Infra   │    │ • Web App       │
│ • Workflows     │    │ • Deploy Apps    │    │ • Cosmos DB     │
│                 │    │ • Health Checks  │    │ • Storage       │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## Prerequisites

### 1. Azure Setup

#### Service Principal Configuration
1. Create an Azure AD App Registration:
   ```bash
   az ad app create --display-name "MemberPropertyAlert-GitHub-Actions"
   ```

2. Create a Service Principal:
   ```bash
   az ad sp create --id <app-id>
   ```

3. Configure OIDC Federation:
   ```bash
   az ad app federated-credential create \
     --id <app-id> \
     --parameters '{
       "name": "GitHub-Actions",
       "issuer": "https://token.actions.githubusercontent.com",
       "subject": "repo:<your-org>/<your-repo>:ref:refs/heads/main",
       "audiences": ["api://AzureADTokenExchange"]
     }'
   ```

4. Assign Azure Permissions:
   ```bash
   # Contributor role for resource management
   az role assignment create \
     --assignee <service-principal-id> \
     --role "Contributor" \
     --scope "/subscriptions/<subscription-id>"
   ```

### 2. GitHub Secrets Configuration

Configure the following secrets in your GitHub repository:

| Secret Name | Description | Example |
|-------------|-------------|---------|
| `AZURE_CLIENT_ID` | Service Principal Application ID | `12345678-1234-1234-1234-123456789012` |
| `AZURE_TENANT_ID` | Azure AD Tenant ID | `87654321-4321-4321-4321-210987654321` |
| `AZURE_SUBSCRIPTION_ID` | Azure Subscription ID | `abcdef12-3456-7890-abcd-ef1234567890` |
| `RENTCAST_API_KEY` | RentCast API Key for property data | `your-rentcast-api-key` |
| `ADMIN_API_KEY` | Admin API Key for secure endpoints | `your-admin-api-key` |

## Workflow Structure

### Main Workflow: `member-property-alert-cd.yml`

The deployment workflow consists of the following jobs:

1. **analyze-changes**: Detects what components have changed
2. **build-and-test**: Builds and tests the application
3. **deploy-infrastructure**: Deploys Azure resources using Bicep
4. **deploy-function-app**: Deploys the Azure Functions application
5. **deploy-web-app**: Deploys the React web application
6. **test-deployments**: Performs health checks
7. **deployment-summary**: Generates deployment report

### Trigger Conditions

The workflow triggers on:
- **Push to main/master**: Automatic deployment to dev environment
- **Manual dispatch**: Choose environment (dev/test/prod)
- **Path-based triggers**: Only when relevant files change

## Environment Configuration

### Resource Naming Convention

Resources follow this naming pattern:
```
<resource-type>-<app-name>-<environment>-<location>-<unique-suffix>
```

Examples:
- `func-member-property-alert-dev-eastus2-abc123`
- `web-member-property-alert-prod-eastus2-def456`
- `rg-member-property-alert-test-eastus2`

### Environment-Specific Settings

| Environment | Resource Group | App Service Plan | Cosmos DB | Backup Retention |
|-------------|----------------|------------------|-----------|------------------|
| **dev** | `rg-member-property-alert-dev-eastus2` | B1 (Basic) | Free Tier | 7 days |
| **test** | `rg-member-property-alert-test-eastus2` | B1 (Basic) | Serverless | 7 days |
| **prod** | `rg-member-property-alert-prod-eastus2` | P1v3 (Premium) | Serverless | 30 days |

## Deployment Process

### 1. Change Detection

The pipeline automatically detects changes in:
- **Infrastructure**: `infra/main.bicep`, workflow files
- **Functions**: `src/MemberPropertyAlert.Core/`, `src/MemberPropertyAlert.Functions/`
- **UI**: `src/MemberPropertyAlert.UI/`

### 2. Build Process

#### .NET Components
```bash
dotnet restore MemberPropertyMarketAlert.sln
dotnet build MemberPropertyMarketAlert.sln --configuration Release
dotnet test MemberPropertyMarketAlert.sln --configuration Release
dotnet publish src/MemberPropertyAlert.Functions/MemberPropertyAlert.Functions.csproj
```

#### React UI
```bash
cd src/MemberPropertyAlert.UI
npm ci
npm test -- --ci --coverage
npm run build
```

### 3. Infrastructure Deployment

Uses Azure Bicep templates with environment-specific parameters:
- Validates template before deployment
- Creates resource group if needed
- Deploys all Azure resources
- Extracts deployment outputs for app deployment

### 4. Application Deployment

#### Function App
- Creates zip package from published output
- Deploys using `az functionapp deploy`
- Supports async deployment for faster pipeline

#### Web App
- Packages React build output
- Deploys using `az webapp deploy`
- Configures proper routing for SPA

### 5. Health Checks

Validates deployments with:
- Function App health endpoint: `/api/health`
- Web App availability check
- Retry logic with reasonable timeouts

## Manual Deployment

### Using GitHub Actions UI

1. Go to **Actions** tab in GitHub
2. Select **Member Property Alert Deployment**
3. Click **Run workflow**
4. Choose environment (dev/test/prod)
5. Click **Run workflow**

### Using Azure CLI

For emergency deployments or troubleshooting:

```bash
# Deploy infrastructure only
az deployment group create \
  --resource-group "rg-member-property-alert-dev-eastus2" \
  --template-file "infra/main.bicep" \
  --parameters @infra/main.dev.parameters.json

# Deploy Function App
az functionapp deploy \
  --resource-group "rg-member-property-alert-dev-eastus2" \
  --name "func-member-property-alert-dev" \
  --src-path "./function-app.zip"
```

## Monitoring and Troubleshooting

### Application Insights

All applications are configured with Application Insights for monitoring:
- **Function App**: Automatic instrumentation
- **Web App**: Client-side telemetry
- **Custom Events**: Business logic tracking

### Common Issues

#### 1. Authentication Failures
```
Error: AADSTS70021: No matching federated identity record found
```
**Solution**: Verify OIDC federation configuration and GitHub repository settings.

#### 2. Resource Naming Conflicts
```
Error: The storage account name 'stmemberpropertyalertdev' is already taken
```
**Solution**: The unique suffix should prevent this, but you may need to adjust naming.

#### 3. Deployment Timeouts
```
Error: The deployment operation timed out
```
**Solution**: Check Azure portal for detailed error messages and resource status.

### Debugging Steps

1. **Check GitHub Actions logs** for detailed error messages
2. **Review Azure Activity Log** for resource-level issues
3. **Examine Application Insights** for runtime issues
4. **Validate Bicep templates** locally using `az bicep build`

## Security Considerations

### Secrets Management

- **Never commit secrets** to the repository
- Use **GitHub Secrets** for CI/CD variables
- Consider **Azure Key Vault** for application secrets
- Rotate secrets regularly

### Access Control

- **Principle of least privilege** for service principals
- **Environment-specific permissions** where possible
- **Regular access reviews** for GitHub repository access

### Network Security

- **HTTPS only** for all web applications
- **Private endpoints** for production databases (future enhancement)
- **Network security groups** for additional protection

## Performance Optimization

### Build Optimization

- **NuGet package caching** reduces restore time
- **npm caching** speeds up Node.js builds
- **Parallel builds** where possible
- **Conditional deployments** based on changes

### Deployment Optimization

- **Async deployments** for faster pipeline completion
- **Health check retries** with exponential backoff
- **Artifact retention** limited to 1 day for storage efficiency

## Future Enhancements

### Planned Improvements

1. **Pull Request Validation**: Add PR workflow for code quality checks
2. **Integration Tests**: Automated testing against deployed environments
3. **Blue-Green Deployments**: Zero-downtime production deployments
4. **Infrastructure Drift Detection**: Monitor for manual changes
5. **Cost Optimization**: Automated resource scaling based on usage

### Monitoring Enhancements

1. **Custom Dashboards**: Environment-specific monitoring views
2. **Alerting Rules**: Proactive issue detection
3. **Performance Baselines**: Track deployment impact on performance
4. **Cost Tracking**: Monitor resource costs by environment

## Support and Maintenance

### Regular Tasks

- **Monthly**: Review and rotate secrets
- **Quarterly**: Update GitHub Actions versions
- **Bi-annually**: Review and optimize resource configurations

### Emergency Procedures

1. **Rollback Process**: Use previous deployment artifacts
2. **Incident Response**: Escalation procedures and contacts
3. **Disaster Recovery**: Cross-region backup and restore procedures

## Conclusion

This CI/CD setup provides a robust, secure, and efficient deployment pipeline for the Member Property Alert project. It follows industry best practices and is designed to scale with the project's growth.

For questions or issues, please refer to the troubleshooting section or contact the development team.
