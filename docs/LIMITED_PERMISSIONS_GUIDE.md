# Limited Permissions Setup Guide

## Current Situation

You're working in an Azure subscription (`ForEverquest`) where you have:
- ✅ **Contributor access** to resource group `rg-member-property-alert-dev`
- ❌ **No permissions** to create service principals or query Graph API
- ✅ **Resources already deployed** (Function App, Cosmos DB, Storage)

## Available Options

### Option 1: Manual Deployment (Recommended for Now)

Since your resources are already deployed, you can manually deploy code changes:

```powershell
# Build and deploy your function app
dotnet publish src/MemberPropertyAlert.Functions -c Release -o publish
az functionapp deployment source config-zip --resource-group "rg-member-property-alert-dev" --name "func-member-property-alert-dev" --src "deploy.zip"
```

### Option 2: Request Administrator Help

Ask your Azure administrator (whoever manages the `Ermey Enterprises` tenant) to:

1. **Create a service principal for CI/CD:**
   ```bash
   az ad sp create-for-rbac \
     --name "MemberPropertyAlert-CI" \
     --role "Contributor" \
     --scopes "/subscriptions/f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6/resourceGroups/rg-member-property-alert-dev"
   ```

2. **Provide you with the JSON output** to configure GitHub secrets

3. **Grant you "Application Administrator" role** in Azure AD (if you need to manage service principals)

### Option 3: Use GitHub OIDC (If Admin Helps)

If your admin creates an Azure AD app registration for you, you can use GitHub OIDC instead of service principals:

```yaml
# .github/workflows/deploy.yml
permissions:
  id-token: write
  contents: read

jobs:
  deploy:
    steps:
    - uses: azure/login@v1
      with:
        client-id: ${{ secrets.AZURE_CLIENT_ID }}
        tenant-id: ${{ secrets.AZURE_TENANT_ID }}
        subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

## Testing Your Current Deployment

Your function app is already deployed. Let's test the available endpoints:

### 1. Check Function App Status
```powershell
# Check if function app is running
Invoke-WebRequest -Uri "https://func-member-property-alert-dev.azurewebsites.net" -Method GET
```

### 2. List Available Functions
```powershell
# Get function list from Azure
az functionapp function list --name "func-member-property-alert-dev" --resource-group "rg-member-property-alert-dev" --output table
```

### 3. Test API Endpoints
```powershell
# Try different endpoint variations
$baseUrl = "https://func-member-property-alert-dev.azurewebsites.net"
$apiKey = "dev-api-key-123456"

# Test root endpoint
Invoke-RestMethod -Uri "$baseUrl" -Headers @{"X-API-Key" = $apiKey}

# Test common function names
Invoke-RestMethod -Uri "$baseUrl/api/GetInstitutions" -Headers @{"X-API-Key" = $apiKey}
Invoke-RestMethod -Uri "$baseUrl/api/institutions" -Headers @{"X-API-Key" = $apiKey}
Invoke-RestMethod -Uri "$baseUrl/api/health" -Headers @{"X-API-Key" = $apiKey}
```

## Next Steps

1. **Immediate**: Test your deployed functions (see commands above)
2. **Short-term**: Contact your Azure admin for service principal creation
3. **Long-term**: Set up proper CI/CD once permissions are resolved

## Workarounds for Development

### Manual Code Deployment

1. **Build your project:**
   ```powershell
   dotnet build src/MemberPropertyAlert.Functions -c Release
   ```

2. **Create deployment package:**
   ```powershell
   # Zip the publish folder
   Compress-Archive -Path "publish/*" -DestinationPath "deploy.zip" -Force
   ```

3. **Deploy manually:**
   ```powershell
   az functionapp deployment source config-zip \
     --resource-group "rg-member-property-alert-dev" \
     --name "func-member-property-alert-dev" \
     --src "deploy.zip"
   ```

### Local Development

1. **Run functions locally:**
   ```powershell
   cd src/MemberPropertyAlert.Functions
   func start
   ```

2. **Test locally first** before manual deployment to Azure

## Contact Information

**Azure Administrator**: Contact whoever manages your `Ermey Enterprises` Azure tenant to request:
- Service principal creation permissions
- Application Administrator role in Azure AD
- Or assistance with creating the required service principal

**Tenant Details:**
- Tenant: `Ermey Enterprises`
- Tenant ID: `e788f8d8-5971-4072-8cd3-06f6957b71f9`
- Domain: `ermeyenterprises.com`
