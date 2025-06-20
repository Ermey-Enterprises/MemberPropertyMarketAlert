# Azure CI/CD Pipeline Setup Guide

## Overview

The CI/CD pipeline requires Azure service principal credentials to deploy resources. This guide explains how to set up the required GitHub Secrets for automated deployment.

## Required GitHub Secrets

The following secrets must be configured in your GitHub repository:

### 1. AZURE_CREDENTIALS
Complete service principal JSON with the following format:
```json
{
  "clientId": "<service-principal-client-id>",
  "clientSecret": "<service-principal-client-secret>",
  "subscriptionId": "<azure-subscription-id>",
  "tenantId": "<azure-tenant-id>"
}
```

### 2. AZURE_SUBSCRIPTION_ID
Your Azure subscription ID (also included in AZURE_CREDENTIALS but used separately)

## Step-by-Step Setup

### Step 1: Create Azure Service Principal

1. **Login to Azure CLI:**
   ```bash
   az login
   ```

2. **Set your subscription:**
   ```bash
   az account set --subscription "<your-subscription-id>"
   ```

3. **Create service principal:**
   ```bash
   az ad sp create-for-rbac \
     --name "MemberPropertyAlert-CI" \
     --role "Contributor" \
     --scopes "/subscriptions/<your-subscription-id>" \
     --sdk-auth
   ```

4. **Copy the output JSON** - this will be your `AZURE_CREDENTIALS` secret

### Step 2: Configure GitHub Secrets

1. **Navigate to your GitHub repository**
2. **Go to Settings → Secrets and variables → Actions**
3. **Add the following repository secrets:**

   - **Name:** `AZURE_CREDENTIALS`
   - **Value:** The complete JSON output from Step 1

   - **Name:** `AZURE_SUBSCRIPTION_ID`
   - **Value:** Your Azure subscription ID

### Step 3: Verify Permissions

Ensure the service principal has the following permissions:
- **Contributor** role on the subscription (for resource creation)
- **User Access Administrator** role (if deploying role assignments)

### Step 4: Test the Pipeline

1. **Push a commit to the main branch**
2. **Check GitHub Actions tab** for pipeline execution
3. **Verify successful Azure login** in the logs

## Alternative: Manual Deployment

If you prefer to deploy manually without CI/CD:

### Option 1: PowerShell Script
```powershell
# Run the deployment script
.\deploy\deploy.ps1 -SubscriptionId "<your-subscription-id>" -ResourceGroupName "MemberPropertyMarketAlert-rg"
```

### Option 2: Azure CLI
```bash
# Create resource group
az group create --name "MemberPropertyMarketAlert-rg" --location "East US"

# Deploy infrastructure
az deployment group create \
  --resource-group "MemberPropertyMarketAlert-rg" \
  --template-file "./infra/main.bicep" \
  --parameters "./infra/main.dev.parameters.json"
```

## Troubleshooting

### Common Issues:

1. **"Login failed with Error: Using auth-type: SERVICE_PRINCIPAL. Not all values are present. Ensure 'client-id' and 'tenant-id' are supplied."**
   - **Root Cause**: The `AZURE_CREDENTIALS` GitHub Secret has not been configured
   - **Solution**: Follow Step 2 above to create and configure the GitHub Secret
   - **Verification**: Check that the secret exists in GitHub → Settings → Secrets and variables → Actions
   - **Format**: Ensure the secret contains valid JSON with all required fields

2. **"Login failed" error (general):**
   - Verify AZURE_CREDENTIALS secret is properly formatted JSON
   - Ensure service principal has correct permissions
   - Check that subscription ID is correct

3. **"Insufficient privileges to complete the operation" during service principal creation:**
   - **Common when**: You don't have Owner/User Access Administrator role in the subscription
   - **Solutions**:
     - **Option A**: Ask your Azure administrator to create the service principal for you
     - **Option B**: Use resource group scope instead of subscription scope:
       ```bash
       az ad sp create-for-rbac \
         --name "MemberPropertyAlert-CI" \
         --role "Contributor" \
         --scopes "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>"
       ```
     - **Option C**: Use Azure DevOps Service Connections or GitHub OIDC (see Alternative Authentication Methods below)

4. **"Failed to query Graph API" or permission errors:**
   - **Root Cause**: Limited Graph API permissions
   - **Solutions**:
     - Try using `--assignee-object-id` instead of email
     - Ask administrator to grant Graph API permissions
     - Use alternative authentication methods

5. **"Resource group not found" error:**
   - Ensure resource group exists or pipeline creates it
   - Check resource group name in parameters

### Debugging Steps:

1. **Validate service principal:**
   ```bash
   az login --service-principal \
     --username "<client-id>" \
     --password "<client-secret>" \
     --tenant "<tenant-id>"
   ```

2. **Test permissions:**
   ```bash
   az group list
   az resource list
   ```

3. **Check GitHub Actions logs** for detailed error messages

## Alternative Authentication Methods

When you don't have sufficient permissions to create service principals, consider these alternatives:

### Option 1: GitHub OIDC (Recommended for limited permissions)

GitHub Actions can authenticate to Azure without storing secrets using OpenID Connect:

1. **Create an Azure AD Application** (if you have permissions):
   ```bash
   az ad app create --display-name "MemberPropertyAlert-OIDC"
   ```

2. **Create federated credentials** for GitHub:
   ```bash
   az ad app federated-credential create \
     --id <app-id> \
     --parameters '{
       "name": "github-main",
       "issuer": "https://token.actions.githubusercontent.com",
       "subject": "repo:your-username/MemberPropertyMarketAlert:ref:refs/heads/main",
       "audiences": ["api://AzureADTokenExchange"]
     }'
   ```

3. **Update GitHub Actions workflow** to use OIDC:
   ```yaml
   permissions:
     id-token: write
     contents: read
   
   steps:
     - uses: azure/login@v1
       with:
         client-id: ${{ secrets.AZURE_CLIENT_ID }}
         tenant-id: ${{ secrets.AZURE_TENANT_ID }}
         subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
   ```

### Option 2: Request Administrator Assistance

If you don't have permissions, ask your Azure administrator to:

1. **Create the service principal for you**:
   ```bash
   az ad sp create-for-rbac \
     --name "MemberPropertyAlert-CI" \
     --role "Contributor" \
     --scopes "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>"
   ```

2. **Provide you with the output JSON** to configure GitHub secrets

3. **Grant necessary permissions** for your specific resource group

### Option 3: Use Azure DevOps

If GitHub Actions permissions are too restrictive:

1. **Set up Azure DevOps project**
2. **Create service connection** using Azure Resource Manager
3. **Use Azure Pipelines** instead of GitHub Actions

### Option 4: Manual Deployment

For development/testing without CI/CD:

1. **Deploy using your personal Azure CLI session**:
   ```bash
   az login
   az account set --subscription "<subscription-id>"
   # Run deployment commands manually
   ```

2. **Use deployment scripts** with your authenticated session

## Security Best Practices

1. **Limit service principal scope** to specific resource groups
2. **Use separate service principals** for different environments
3. **Rotate credentials regularly**
4. **Monitor service principal usage** in Azure AD logs
5. **Use managed identities** where possible instead of service principals

## Environment-Specific Configuration

### Development Environment
- Resource Group: `MemberPropertyMarketAlert-dev-rg`
- Function App: `func-member-property-alert-dev`
- Storage Account: `stmemberpropertydev`

### Production Environment
- Resource Group: `MemberPropertyMarketAlert-prod-rg`
- Function App: `func-member-property-alert-prod`
- Storage Account: `stmemberpropertyprod`

## Next Steps

Once Azure credentials are configured:

1. **Push to main branch** to trigger deployment
2. **Monitor GitHub Actions** for successful deployment
3. **Verify Azure resources** are created correctly
4. **Test API endpoints** and admin UI
5. **Configure application settings** (RentCast API key, etc.)

## Support

For issues with Azure setup:
- Check [Azure CLI documentation](https://docs.microsoft.com/en-us/cli/azure/)
- Review [GitHub Actions Azure login](https://github.com/Azure/login)
- Consult [Azure RBAC documentation](https://docs.microsoft.com/en-us/azure/role-based-access-control/)
