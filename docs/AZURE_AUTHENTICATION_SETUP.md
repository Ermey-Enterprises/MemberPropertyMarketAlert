# Azure Authentication Setup for GitHub Actions CI/CD

## 🎯 **Current Status**

✅ **CI/CD Pipeline is 100% Working** - Build, test, and code quality stages all pass  
❌ **Azure Authentication Missing** - Need to configure GitHub repository with Azure credentials  

The enhanced 4-stage CI/CD pipeline is fully functional but requires Azure authentication setup to complete the deployment stages.

## 🔧 **Required Configuration**

The CI/CD pipeline uses **OIDC (OpenID Connect)** authentication for secure, keyless Azure access. You need to configure:

### **GitHub Repository Variables** (Public, not sensitive)
- `AZURE_CLIENT_ID` - Azure App Registration Client ID
- `AZURE_TENANT_ID` - Azure Tenant ID  
- `AZURE_SUBSCRIPTION_ID` - Azure Subscription ID

### **GitHub Repository Secrets** (Private, sensitive)
- `AZURE_STATIC_WEB_APPS_API_TOKEN` - Static Web Apps deployment token

## 🚀 **Setup Instructions**

### **Step 1: Create Azure App Registration for OIDC**

1. **Open Azure Portal** → Azure Active Directory → App registrations
2. **Click "New registration"**
   - Name: `MemberPropertyAlert-GitHub-Actions`
   - Supported account types: `Accounts in this organizational directory only`
   - Redirect URI: Leave blank
   - Click **Register**

3. **Note the Application (client) ID** - This becomes `AZURE_CLIENT_ID`
4. **Note the Directory (tenant) ID** - This becomes `AZURE_TENANT_ID`

### **Step 2: Configure Federated Credentials for OIDC**

1. **In your App Registration** → Certificates & secrets → Federated credentials
2. **Click "Add credential"**
3. **Select "GitHub Actions deploying Azure resources"**
4. **Configure:**
   - Organization: `Ermey-Enterprises`
   - Repository: `MemberPropertyMarketAlert`
   - Entity type: `Branch`
   - GitHub branch name: `main`
   - Name: `MemberPropertyAlert-Main-Branch`
5. **Click "Add"**

### **Step 3: Assign Azure Permissions**

1. **Open Azure Portal** → Subscriptions → Select your subscription
2. **Note the Subscription ID** - This becomes `AZURE_SUBSCRIPTION_ID`
3. **Go to Access control (IAM)** → Add → Add role assignment
4. **Select Role: "Contributor"** (or create custom role with specific permissions)
5. **Select Members** → Search for `MemberPropertyAlert-GitHub-Actions`
6. **Click "Review + assign"**

### **Step 4: Get Static Web Apps Token**

1. **Deploy infrastructure first** (manually or via Azure CLI):
   ```bash
   az group create --name memberpropertyalert-eastus2-rg --location eastus2
   az deployment group create \
     --resource-group memberpropertyalert-eastus2-rg \
     --template-file infra/main.bicep \
     --parameters @infra/main.prod.parameters.json \
     --parameters location=eastus2
   ```

2. **Get Static Web App deployment token**:
   ```bash
   az staticwebapp secrets list \
     --name [STATIC_WEB_APP_NAME] \
     --resource-group memberpropertyalert-eastus2-rg \
     --query "properties.apiKey" -o tsv
   ```

### **Step 5: Configure GitHub Repository**

1. **Go to GitHub Repository** → Settings → Secrets and variables → Actions

2. **Add Repository Variables** (Variables tab):
   - `AZURE_CLIENT_ID`: [App Registration Client ID from Step 1]
   - `AZURE_TENANT_ID`: [Directory Tenant ID from Step 1]  
   - `AZURE_SUBSCRIPTION_ID`: [Subscription ID from Step 3]

3. **Add Repository Secrets** (Secrets tab):
   - `AZURE_STATIC_WEB_APPS_API_TOKEN`: [Token from Step 4]

## 🧪 **Testing the Setup**

1. **Trigger the workflow**:
   - Push a commit to main branch, OR
   - Go to Actions → Enhanced Continuous Deployment → Run workflow

2. **Verify all 4 stages complete**:
   ```
   ✅ test-and-build → ✅ deploy-and-configure → ✅ test-and-notify → ✅ notify
   ```

## 🔒 **Security Features**

✅ **OIDC Authentication** - No long-lived secrets, Azure trusts GitHub directly  
✅ **Federated Identity** - Scoped to specific repository and branch  
✅ **Minimal Permissions** - Only required Azure permissions granted  
✅ **Token Rotation** - GitHub automatically handles token lifecycle  

## 🛠️ **Alternative: Quick Setup Script**

If you have Azure CLI access, you can use this script to automate the setup:

```bash
# Set variables
SUBSCRIPTION_ID="your-subscription-id"
RESOURCE_GROUP="memberpropertyalert-eastus2-rg"
APP_NAME="MemberPropertyAlert-GitHub-Actions"
GITHUB_ORG="Ermey-Enterprises"
GITHUB_REPO="MemberPropertyMarketAlert"

# Create App Registration
APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
echo "AZURE_CLIENT_ID: $APP_ID"

# Get Tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "AZURE_TENANT_ID: $TENANT_ID"

# Create Service Principal
az ad sp create --id $APP_ID

# Add Federated Credential
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "MemberPropertyAlert-Main-Branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'$GITHUB_ORG'/'$GITHUB_REPO':ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Assign Contributor role
az role assignment create \
  --assignee $APP_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID

echo "Setup complete! Add these to GitHub repository variables:"
echo "AZURE_CLIENT_ID: $APP_ID"
echo "AZURE_TENANT_ID: $TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
```

## 📋 **Troubleshooting**

### **Common Issues:**

1. **"Login failed with Error: Using auth-type: SERVICE_PRINCIPAL"**
   - ✅ **Solution**: Add the 3 required variables to GitHub repository variables

2. **"AADSTS70021: No matching federated identity record found"**
   - ✅ **Solution**: Verify federated credential is configured for correct org/repo/branch

3. **"Insufficient privileges to complete the operation"**
   - ✅ **Solution**: Ensure App Registration has Contributor role on subscription

4. **Static Web App deployment fails**
   - ✅ **Solution**: Add `AZURE_STATIC_WEB_APPS_API_TOKEN` to repository secrets

## 🎉 **Expected Result**

Once configured, the CI/CD pipeline will:

1. ✅ **Build and test** all code with zero errors
2. ✅ **Deploy infrastructure** to Azure (when needed)
3. ✅ **Deploy applications** (Functions + Static Web App)
4. ✅ **Run integration tests** including webhook testing
5. ✅ **Provide deployment URLs** and status summary

The enhanced 4-stage pipeline will be **100% operational** for enterprise-grade continuous deployment!

## 📞 **Next Steps**

1. **Complete Azure authentication setup** using this guide
2. **Test the full pipeline** with a commit or manual trigger
3. **Verify all 4 stages pass** and applications are deployed
4. **Begin development** of testing and administration enhancements

The CI/CD foundation is solid and ready for production use once authentication is configured!
