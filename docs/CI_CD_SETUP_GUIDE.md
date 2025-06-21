# CI/CD Setup Guide - Member Property Market Alert

This guide will help you set up continuous integration and deployment for the Member Property Market Alert project.

## 🎯 **Why CI/CD Only?**

This project uses **CI/CD-driven infrastructure** exclusively:

✅ **No manual scripts needed** - All infrastructure is managed via Bicep templates
✅ **Version controlled** - Infrastructure changes are tracked in Git
✅ **Consistent deployments** - Same process for all environments
✅ **Automated testing** - Build and test before deployment
✅ **Rollback capability** - Easy to revert to previous versions

**Previous manual PowerShell scripts have been removed** to prevent configuration drift and ensure all deployments go through the proper CI/CD pipeline.

## 🎯 **Prerequisites**

1. **GitHub Repository**: `https://github.com/Ermey-Enterprises/MemberPropertyMarketAlert.git`
2. **Azure Subscription**: Access to create resources
3. **Azure Resource Group**: `rg-member-property-alert-dev` (already exists)

---

## 🔧 **Step 1: Azure Service Principal Setup**

### **Create Azure Service Principal for OIDC**

```powershell
# Set variables
$subscriptionId = "f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6"
$resourceGroup = "rg-member-property-alert-dev"
$appName = "github-memberpropertyalert-cicd"

# Login to Azure
az login
az account set --subscription $subscriptionId

# Create service principal
$sp = az ad sp create-for-rbac --name $appName --role Contributor --scopes "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup" --sdk-auth --json | ConvertFrom-Json

# Enable OIDC
az ad app federated-credential create --id $sp.clientId --parameters @- <<EOF
{
  "name": "github-actions",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:Ermey-Enterprises/MemberPropertyMarketAlert:ref:refs/heads/main",
  "description": "GitHub Actions OIDC for main branch",
  "audiences": ["api://AzureADTokenExchange"]
}
EOF

# Display values for GitHub configuration
Write-Host "=== GitHub Repository Variables ===" -ForegroundColor Green
Write-Host "AZURE_CLIENT_ID: $($sp.clientId)"
Write-Host "AZURE_TENANT_ID: $($sp.tenantId)"
Write-Host "AZURE_SUBSCRIPTION_ID: $($sp.subscriptionId)"
```

---

## 🔑 **Step 2: GitHub Repository Configuration**

### **Set Repository Variables**

Go to your GitHub repository → Settings → Secrets and variables → Actions → Variables tab:

| Variable Name | Value | Description |
|---------------|-------|-------------|
| `AZURE_CLIENT_ID` | `[from service principal output]` | Azure App Registration Client ID |
| `AZURE_TENANT_ID` | `[from service principal output]` | Azure Tenant ID |
| `AZURE_SUBSCRIPTION_ID` | `f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6` | Azure Subscription ID |

### **Set Repository Secrets**

Go to your GitHub repository → Settings → Secrets and variables → Actions → Secrets tab:

| Secret Name | Value | Description |
|-------------|-------|-------------|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | `[See Step 3]` | Static Web App deployment token |

---

## 🌐 **Step 3: Static Web App API Token**

### **Option A: Get from Existing Static Web App**
```powershell
# If you already have a Static Web App deployed
az staticwebapp secrets list --name "swa-member-property-alert-dev" --resource-group "rg-member-property-alert-dev" --query "properties.apiKey" -o tsv
```

### **Option B: Will be Generated During First Deployment**
The CI/CD will attempt to deploy infrastructure first, which will create the Static Web App. Then you'll need to:
1. Run the workflow once (it may fail on Static Web App deployment)
2. Get the API token using Option A above
3. Add it to GitHub secrets
4. Re-run the workflow

---

## 🚀 **Step 4: Verify CI/CD Configuration**

### **Current CI/CD Features**

✅ **Triggers**:
- Push to `main` branch
- Manual workflow dispatch

✅ **Build Process**:
- .NET 8.0 solution build
- React UI build (Node.js 18.x)
- Unit tests execution
- Code formatting check

✅ **Deployment**:
- Azure infrastructure (Bicep)
- Azure Functions
- Static Web App (React UI)

✅ **Smart Deployment**:
- Infrastructure only deployed when `/infra/` files change
- Application code deployed every time

### **Fixed Issues**
- ✅ Resource group name corrected to `rg-member-property-alert-dev`
- ✅ Parameter file corrected to `main.dev.parameters.json`
- ✅ Azure location set to `eastus` to match existing resources

---

## 🧪 **Step 5: Test CI/CD**

### **Manual Test**
1. Go to GitHub repository → Actions tab
2. Select "Enhanced Continuous Deployment"
3. Click "Run workflow" → "Run workflow"
4. Monitor the deployment progress

### **Automatic Test**
1. Make a small change to any file
2. Commit and push to `main` branch
3. CI/CD will automatically trigger

---

## 🔍 **Troubleshooting**

### **Common Issues**

#### **1. Azure Authentication Fails**
```
Error: AADSTS70002: The request body must contain the following parameter: 'client_assertion'
```
**Solution**: Verify OIDC federated credential is created correctly with exact repository path.

#### **2. Static Web App Deployment Fails**
```
Error: Azure Static Web Apps API token is invalid
```
**Solution**: Get API token from Azure portal and add to GitHub secrets.

#### **3. Resource Not Found**
```
Error: Resource group 'rg-member-property-alert-dev' could not be found
```
**Solution**: Verify resource group exists and service principal has Contributor access.

#### **4. Infrastructure Deployment Skipped**
If infrastructure isn't deploying when expected:
- Check if files in `/infra/` directory were modified
- Use manual workflow dispatch to force infrastructure deployment

### **Monitoring**
```powershell
# Check resource group status
az group show --name "rg-member-property-alert-dev"

# Check function app status
az functionapp show --name "func-member-property-alert-dev" --resource-group "rg-member-property-alert-dev" --query "{name:name, state:state, hostNames:hostNames}"

# Check static web app status
az staticwebapp show --name "swa-member-property-alert-dev" --resource-group "rg-member-property-alert-dev" --query "{name:name, defaultHostname:defaultHostname}"
```

---

## 📋 **Quick Setup Checklist**

- [ ] **Azure Service Principal created** with OIDC federated credential
- [ ] **GitHub Variables set**: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- [ ] **GitHub Secret set**: `AZURE_STATIC_WEB_APPS_API_TOKEN` (may need to do after first deployment)
- [ ] **Repository permissions**: Service principal has Contributor access to resource group
- [ ] **First deployment tested**: Manual workflow dispatch completed successfully
- [ ] **Automatic deployment tested**: Push to main branch triggers CI/CD

---

## 🔗 **Related Documentation**

- [Azure Resources Reference](AZURE_RESOURCES_REFERENCE.md)
- [Project Summary](PROJECT_SUMMARY.md)
- [Deployment Guide](DEPLOYMENT_GUIDE.md)

---

## 💡 **Next Steps**

Once CI/CD is working:
1. **Set up environments**: Create test and prod parameter files
2. **Branch protection**: Require PR reviews before merging to main
3. **Monitoring**: Set up Azure Monitor alerts
4. **Secrets management**: Use Azure Key Vault for sensitive configuration
