# Infrastructure Deployment Guide

This guide provides step-by-step instructions to deploy the updated infrastructure with the corrected CosmosDB containers.

## Prerequisites

1. **Azure CLI** installed and configured
2. **Appropriate Azure permissions** to create resources
3. **Resource group** identified or created

## Quick Deployment Commands

### Option 1: Using the Deployment Script (Recommended)

```powershell
# Navigate to project directory
cd c:/Users/micha/MemberPropertyMarketAlert

# Run a what-if analysis first (optional but recommended)
.\scripts\Deploy-Infrastructure.ps1 -ResourceGroupName "YOUR_RESOURCE_GROUP_NAME" -WhatIf

# Deploy the infrastructure
.\scripts\Deploy-Infrastructure.ps1 -ResourceGroupName "YOUR_RESOURCE_GROUP_NAME"
```

### Option 2: Direct Azure CLI Commands

```bash
# Login to Azure (if not already logged in)
az login

# Deploy the infrastructure
az deployment group create \
  --resource-group "YOUR_RESOURCE_GROUP_NAME" \
  --template-file "infra/main.bicep" \
  --parameters "environment=dev" \
  --parameters "@infra/main.dev.parameters.json"
```

## Step-by-Step Instructions

### Step 1: Identify Your Resource Group

You need to know your Azure resource group name. If you don't have one, you can create it:

```bash
# Create a new resource group (if needed)
az group create --name "rg-member-property-alert-dev" --location "East US"
```

### Step 2: Run What-If Analysis (Optional)

Before deploying, you can see what changes will be made:

```powershell
.\scripts\Deploy-Infrastructure.ps1 -ResourceGroupName "YOUR_RESOURCE_GROUP_NAME" -WhatIf
```

This will show you:
- What resources will be created
- What resources will be modified
- What containers will be added to CosmosDB

### Step 3: Deploy the Infrastructure

```powershell
.\scripts\Deploy-Infrastructure.ps1 -ResourceGroupName "YOUR_RESOURCE_GROUP_NAME"
```

The script will:
1. ✅ Check Azure CLI installation and login status
2. ✅ Verify or create the resource group
3. ✅ Deploy the updated Bicep template
4. ✅ Create the missing CosmosDB containers:
   - `Alerts` (partition: `/institutionId`)
   - `ScanLogs` (partition: `/institutionId`)
5. ✅ Fix any naming mismatches
6. ✅ Display deployment results and next steps

### Step 4: Verify the Deployment

After deployment, verify the containers exist:

```powershell
# List all containers in your CosmosDB
az cosmosdb sql container list \
  --resource-group "YOUR_RESOURCE_GROUP_NAME" \
  --account-name "YOUR_COSMOS_ACCOUNT_NAME" \
  --database-name "MemberPropertyAlert"
```

You should see these containers:
- `Institutions` (partition: `/id`)
- `Addresses` (partition: `/institutionId`)
- `Alerts` (partition: `/institutionId`)
- `ScanLogs` (partition: `/institutionId`)

## Common Resource Group Names

Based on typical naming conventions, your resource group might be named:
- `rg-member-property-alert-dev`
- `rg-member-property-alert-test`
- `rg-member-property-alert-prod`
- `MemberPropertyAlert-dev`
- `MemberPropertyAlert-test`
- `MemberPropertyAlert-prod`

## Finding Your Resource Group

If you're not sure of your resource group name:

```bash
# List all resource groups
az group list --query "[].name" -o table

# Search for resource groups containing "member" or "property"
az group list --query "[?contains(name, 'member') || contains(name, 'property')].name" -o table
```

## Finding Your CosmosDB Account

To find your CosmosDB account name:

```bash
# List CosmosDB accounts in a specific resource group
az cosmosdb list --resource-group "YOUR_RESOURCE_GROUP_NAME" --query "[].name" -o table

# List all CosmosDB accounts in your subscription
az cosmosdb list --query "[].{Name:name, ResourceGroup:resourceGroup}" -o table
```

## Troubleshooting

### Common Issues

1. **"Resource group not found"**
   - Verify the resource group name is correct
   - Ensure you have access to the subscription

2. **"Insufficient permissions"**
   - You need Contributor or Owner role on the resource group
   - Contact your Azure administrator

3. **"Template validation failed"**
   - Ensure you're using the latest Bicep template
   - Check that all required parameters are provided

4. **"Container already exists"**
   - This is normal if containers already exist
   - The deployment will update existing containers if needed

### Getting Help

If you encounter issues:

1. **Check the deployment logs** in the Azure portal
2. **Run with --verbose** for more detailed output:
   ```bash
   az deployment group create --verbose [other parameters]
   ```
3. **Use the what-if option** to preview changes before deploying

## Next Steps After Deployment

1. **Verify containers** exist in the Azure portal
2. **Deploy your application code** to the updated infrastructure
3. **Test the application** to ensure all containers are accessible
4. **Monitor the application logs** for any CosmosDB-related errors

## Example Complete Command

Replace `YOUR_RESOURCE_GROUP_NAME` with your actual resource group name:

```powershell
# Complete example for dev environment
.\scripts\Deploy-Infrastructure.ps1 -ResourceGroupName "rg-member-property-alert-dev" -Environment "dev"
```

This will deploy the corrected infrastructure and resolve the missing CosmosDB container issues.
