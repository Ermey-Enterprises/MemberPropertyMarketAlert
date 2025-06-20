# CI/CD Setup Guide

This guide explains how to set up Continuous Integration and Continuous Deployment (CI/CD) for the Member Property Market Alert application using GitHub Actions and Azure.

## Prerequisites

Before setting up CI/CD, ensure you have:

1. **Azure Subscription** with Owner or Contributor permissions
2. **GitHub Repository** with the application code
3. **Azure CLI** installed locally for initial setup

## Azure Service Principal Setup

### 1. Create Service Principal

Create a service principal for GitHub Actions to authenticate with Azure:

```bash
# Login to Azure
az login

# Set your subscription (replace with your subscription ID)
az account set --subscription "your-subscription-id"

# Create service principal
az ad sp create-for-rbac \
  --name "sp-memberpropertyalert-github" \
  --role "Contributor" \
  --scopes "/subscriptions/your-subscription-id" \
  --sdk-auth
```

Save the JSON output - you'll need it for GitHub secrets.

### 2. Grant Additional Permissions

The service principal needs additional permissions for certain Azure resources:

```bash
# Get the service principal object ID
SP_OBJECT_ID=$(az ad sp show --id "sp-memberpropertyalert-github" --query "id" --output tsv)

# Grant Key Vault access
az role assignment create \
  --assignee $SP_OBJECT_ID \
  --role "Key Vault Contributor" \
  --scope "/subscriptions/your-subscription-id"

# Grant Container Registry access
az role assignment create \
  --assignee $SP_OBJECT_ID \
  --role "AcrPush" \
  --scope "/subscriptions/your-subscription-id"
```

## GitHub Repository Secrets

Navigate to your GitHub repository → Settings → Secrets and variables → Actions, and add these secrets:

### Required Secrets

| Secret Name | Description | Example |
|-------------|-------------|---------|
| `AZURE_CREDENTIALS` | Service principal JSON from step 1 | `{"clientId": "...", "clientSecret": "...", ...}` |
| `AZURE_SUBSCRIPTION_ID` | Your Azure subscription ID | `12345678-1234-1234-1234-123456789012` |

### Optional Secrets (for container registry)

| Secret Name | Description |
|-------------|-------------|
| `ACR_USERNAME` | Azure Container Registry username |
| `ACR_PASSWORD` | Azure Container Registry password |

## Environment Configuration

### 1. GitHub Environments

Create environments in your GitHub repository:

1. Go to Settings → Environments
2. Create environments: `dev`, `test`, `prod`
3. Configure protection rules for `prod`:
   - Required reviewers
   - Deployment branches (restrict to `main`)

### 2. Environment Variables

For each environment, you can set specific variables:

- Navigate to the environment in GitHub
- Add environment variables as needed
- Example variables:
  - `ADMIN_EMAIL`: admin email for that environment
  - `LOCATION`: Azure region for deployment

## Bicep Parameter Files

Ensure you have parameter files for each environment:

- `infra/main.dev.parameters.json`
- `infra/main.test.parameters.json` 
- `infra/main.prod.parameters.json`

Example `main.prod.parameters.json`:

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environment": {
      "value": "prod"
    },
    "location": {
      "value": "East US"
    },
    "baseName": {
      "value": "memberpropertyalert"
    },
    "adminEmail": {
      "value": "admin@yourcompany.com"
    }
  }
}
```

## Workflow Overview

The CI/CD pipeline consists of several workflows:

### 1. Main CI/CD Pipeline (`ci-cd.yml`)

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main`
- Manual dispatch

**Jobs:**
1. **Build and Test**: Compiles code, runs unit tests
2. **Deploy Infrastructure**: Deploys Azure resources using Bicep
3. **Deploy Applications**: Deploys Function App and Web App
4. **Integration Tests**: Runs tests against deployed applications

**Environment Mapping:**
- `main` branch → `prod` environment
- `develop` branch → `test` environment
- Manual dispatch → selectable environment

### 2. Infrastructure Only (`infrastructure.yml`)

**Triggers:**
- Manual dispatch only

**Actions:**
- Deploy: Create/update infrastructure
- What-if: Preview changes without deployment
- Destroy: Delete all resources (use with caution!)

### 3. Container Build (`container-build.yml`)

**Triggers:**
- Push to `main` or `develop` (when src/ changes)
- Pull requests
- Manual dispatch

**Actions:**
- Builds Docker images for Functions and Web apps
- Pushes to Azure Container Registry
- Runs security scans

## Local Deployment

For local testing of the Bicep templates:

```powershell
# Preview deployment
.\deploy-bicep.ps1 -Environment dev -WhatIf

# Deploy to dev environment
.\deploy-bicep.ps1 -Environment dev

# Deploy to specific subscription
.\deploy-bicep.ps1 -Environment dev -SubscriptionId "your-subscription-id"
```

## Monitoring and Alerts

### Application Insights

All deployments include Application Insights for monitoring:

- **Metrics**: Track custom business metrics
- **Logs**: Centralized logging from all services
- **Alerts**: Set up alerts for critical issues

### Health Checks

The pipeline includes health check endpoints:

- Function App: `/api/health`
- Web App: `/health`

These are automatically tested after deployment.

## Security Best Practices

### 1. Secrets Management

- Store sensitive configuration in Azure Key Vault
- Use managed identities for service-to-service authentication
- Rotate secrets regularly

### 2. Network Security

- Deploy services in Virtual Networks (VNet)
- Use private endpoints for Azure services
- Implement Network Security Groups (NSGs)

### 3. Access Control

- Use least-privilege access for service principals
- Implement Role-Based Access Control (RBAC)
- Regular access reviews

## Troubleshooting

### Common Issues

#### 1. Authentication Failures

```bash
# Verify service principal
az ad sp show --id "sp-memberpropertyalert-github"

# Test authentication
az login --service-principal \
  --username "client-id" \
  --password "client-secret" \
  --tenant "tenant-id"
```

#### 2. Permission Issues

```bash
# Check role assignments
az role assignment list --assignee "service-principal-object-id"

# Add missing permissions
az role assignment create \
  --assignee "service-principal-object-id" \
  --role "Contributor" \
  --scope "/subscriptions/subscription-id"
```

#### 3. Template Validation Errors

```bash
# Validate locally
az deployment group validate \
  --resource-group MemberPropertyMarketAlert-rg \
  --template-file infra/main.bicep \
  --parameters infra/main.dev.parameters.json
```

### Debugging Deployments

1. **Check GitHub Actions logs**: Look for specific error messages
2. **Azure Activity Log**: Review deployment activities in Azure Portal
3. **Resource deployment history**: Check individual resource deployment status

## Maintenance

### Regular Tasks

1. **Update dependencies**: Keep NuGet packages and GitHub Actions updated
2. **Review secrets**: Rotate Azure service principal credentials
3. **Monitor costs**: Review Azure spending and optimize resources
4. **Security updates**: Apply security patches and updates

### Scaling Considerations

- **Multi-region deployment**: Modify Bicep for multiple regions
- **Environment-specific SKUs**: Use different service tiers per environment
- **Cost optimization**: Implement auto-scaling and shutdown policies

## Support

For CI/CD pipeline issues:

1. Check GitHub Actions logs
2. Review Azure deployment history
3. Validate Bicep templates locally
4. Consult Azure documentation for service-specific issues

Remember to update this documentation as the pipeline evolves!
